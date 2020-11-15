﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using E_Token.GoogleSignInPlugin;
using Foundation;
using Google.SignIn;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(GoogleClientManager))]
namespace E_Token.GoogleSignInPlugin
{
    /// <summary>
    /// Implementation for GoogleClient
    /// </summary>
    public class GoogleClientManager : NSObject, IGoogleClientManager, ISignInDelegate
    {
        /*
        public DateTime TokenExpirationDate { get { return _tokenExpirationDate; } }
        DateTime _tokenExpirationDate { get; set; }
        */
        static TaskCompletionSource<GoogleResponse> _loginTcs;
        
        EventHandler _onLogout;

        // Class Debug Tag
        private String Tag = typeof(GoogleClientManager).FullName;

        public string IdToken { get { return _idToken; } }
        public string AccessToken { get { return _accessToken; } }
        static string _idToken { get; set; }
        static string _accessToken { get; set; }
        static string _clientId { get; set; }

        public GoogleUser CurrentUser
        {
            get
            {
                if (SignIn.SharedInstance.HasPreviousSignIn)
                    SignIn.SharedInstance.RestorePreviousSignIn();

                var user = SignIn.SharedInstance.CurrentUser;
                return user!=null? new GoogleUser
                {
                    Id = user.UserId,
                    Name = user.Profile.Name,
                    GivenName = user.Profile.GivenName,
                    FamilyName = user.Profile.FamilyName,
                    Email = user.Profile.Email,
                    Picture = user.Profile.HasImage
                        ? new Uri(user.Profile.GetImageUrl(500).ToString())
                        : new Uri(string.Empty)
                }: null;
            }
        }

        public bool IsLoggedIn
        {
            get
            {
                return SignIn.SharedInstance.HasPreviousSignIn;
            }
        }

        public static void Initialize(
            string clientId = null,
            params string[] scopes
        )
        {
            SignIn.SharedInstance.Delegate = DependencyService.Get<IGoogleClientManager>() as ISignInDelegate;
            if (scopes != null && scopes.Length > 0)
            {

                var currentScopes = SignIn.SharedInstance.Scopes;
                var initScopes = currentScopes
                    .Concat(scopes)
                    .Distinct()
                    .ToArray();


                SignIn.SharedInstance.Scopes = initScopes;
            }

            SignIn.SharedInstance.ClientId = string.IsNullOrWhiteSpace(clientId)
                ? GetClientIdFromGoogleServiceDictionary()
                : clientId;
            //SignIn.SharedInstance.ShouldFetchBasicProfile = true;
        }

        static string GetClientIdFromGoogleServiceDictionary()
        {
            var googleServiceDictionary = NSDictionary.FromFile("GoogleService-Info.plist");
            _clientId = googleServiceDictionary["CLIENT_ID"].ToString();
            return googleServiceDictionary["CLIENT_ID"].ToString();
        }

        public event EventHandler OnLogout
        {
            add => _onLogout += value;
            remove => _onLogout -= value;
        }

        public void Login()
        {
            UpdatePresentedViewController();
           
            SignIn.SharedInstance.SignInUser();
        }

        public async Task<GoogleResponse> LoginAsync()
        {
            if (SignIn.SharedInstance.ClientId == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }


            _loginTcs = new TaskCompletionSource<GoogleResponse>();

            UpdatePresentedViewController();
            if (CurrentUser == null)
            {
               
                SignIn.SharedInstance.SignInUser();
            }
            else
            {
                SignIn.SharedInstance.CurrentUser.Authentication.GetTokens(async (Authentication authentication, NSError error) =>
                {
                    if (error == null)
                    {
                        _accessToken = authentication.AccessToken;
                        _idToken = authentication.IdToken;
                        System.Console.WriteLine($"Id Token: {_idToken}");
                        System.Console.WriteLine($"Access Token: {_accessToken}");
                    }

                });

                // Log the result of the authentication
                Debug.WriteLine(Tag + ": Authentication " + GoogleActionStatus.Completed);

                // Send the result to the receivers
                _loginTcs.TrySetResult(new GoogleResponse(CurrentUser,
                    GoogleActionStatus.Completed));
            }

            return await _loginTcs.Task;
        }

        public async Task<GoogleResponse> SilentLoginAsync()
        {
            if (SignIn.SharedInstance.ClientId == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }

            //SignIn.SharedInstance.CurrentUser.Authentication.ClientId != _clientId
            _loginTcs = new TaskCompletionSource<GoogleResponse>();

            if (SignIn.SharedInstance.HasPreviousSignIn)
                SignIn.SharedInstance.RestorePreviousSignIn();

            var currentUser = SignIn.SharedInstance.CurrentUser;
            var isSuccessful = currentUser != null;

            if(isSuccessful)
            {
                OnSignInSuccessful(currentUser);
            }
            else
            {
                _loginTcs.TrySetException(new GoogleClientBaseException());
            }

            return await _loginTcs.Task;
        }

        public static bool OnOpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            var openUrlOptions = new UIApplicationOpenUrlOptions(options);
            return SignIn.SharedInstance.HandleUrl(url);
        }

        public void Logout()
        {
            if (SignIn.SharedInstance.ClientId == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException.ClientNotInitializedErrorMessage);
            }

            if (IsLoggedIn)
            {
                _idToken = null;
                _accessToken = null;
                SignIn.SharedInstance.SignOutUser();
                // Send the logout result to the receivers
                OnLogoutCompleted(EventArgs.Empty);
            }

        }

        public Task LogoutAsync()
        {
            throw new NotImplementedException();
        }

        public void RevokeAccess()
        {
            throw new NotImplementedException();
        }

        public Task RevokeAccessAsync()
        {
            throw new NotImplementedException();
        }

        protected virtual void OnLogoutCompleted(EventArgs e)
        {
            _onLogout?.Invoke(this, e);
        }

        public void DidSignIn(SignIn signIn, Google.SignIn.GoogleUser user, NSError error)
        {
            var isSuccessful = user != null && error == null;

            if (isSuccessful)
            {
                OnSignInSuccessful(user);
                return;
            }

            Exception exception = null;
            switch (error.Code)
            {
                case -1:
                    exception=new GoogleClientSignInUnknownErrorException();
                    break;
                case -2:
                    exception = new GoogleClientSignInKeychainErrorException();
                    break;
                case -3:
                    exception = new GoogleClientSignInNoSignInHandlersInstalledErrorException();
                    break;
                case -4:
                    exception = new GoogleClientSignInHasNoAuthInKeychainErrorException();
                    break;
                case -5:
                    exception = new GoogleClientSignInCanceledErrorException();
                    break;
                default:
                    exception = new GoogleClientBaseException();
                    break;
            }

            _loginTcs.TrySetException(exception);
        }

        [Export("signIn:didDisconnectWithUser:withError:")]
        public void DidDisconnect(SignIn signIn, Google.SignIn.GoogleUser user, NSError error)
        {
            // Perform any operations when the user disconnects from app here.
        }


        void UpdatePresentedViewController()
        {
            var window = UIApplication.SharedApplication.KeyWindow;
            var viewController = window.RootViewController;
            while (viewController.PresentedViewController != null)
            {
                viewController = viewController.PresentedViewController;
            }

            SignIn.SharedInstance.PresentingViewController = viewController;
        }


        void OnSignInSuccessful(Google.SignIn.GoogleUser user)
        {
            GoogleUser googleUser = new GoogleUser
                {
                    Id = user.UserId,
                    Name = user.Profile.Name,
                    GivenName = user.Profile.GivenName,
                    FamilyName = user.Profile.FamilyName,
                    Email = user.Profile.Email,
                    Picture = user.Profile.HasImage
                        ? new Uri(user.Profile.GetImageUrl(500).ToString())
                        : new Uri(string.Empty)
                };

                 user.Authentication.GetTokens(async (Authentication authentication, NSError error) =>
                {
                    if(error ==null)
                    {
                        _accessToken = authentication.AccessToken;
                        _idToken = authentication.IdToken;
                        System.Console.WriteLine($"Id Token: {_idToken}");
                        System.Console.WriteLine($"Access Token: {_accessToken}");
                    }
             
                });

                 // Log the result of the authentication
                Debug.WriteLine(Tag + ": Authentication " + GoogleActionStatus.Completed);

                _loginTcs.TrySetResult(new GoogleResponse(googleUser, 
                    GoogleActionStatus.Completed));
                
        }
    }
}
