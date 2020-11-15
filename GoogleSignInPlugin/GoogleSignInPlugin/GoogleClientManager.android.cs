using System;
using System.Threading.Tasks;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.SignIn;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Tasks;
using E_Token.GoogleSignInPlugin;
using Java.Interop;
using Java.Lang;
using Xamarin.Forms;
using Exception = System.Exception;
using Task = System.Threading.Tasks.Task;

[assembly: Dependency(typeof(GoogleClientManager))]
namespace E_Token.GoogleSignInPlugin
{
    /// <summary>
    /// Implementation for GoogleClient for android.
    /// </summary>
    public class GoogleClientManager : Java.Lang.Object, IGoogleClientManager, IOnCompleteListener
    {
        private static int _requestCode;
        private static string _serverClientId;
        private static string _clientId;
        private static string[] _initScopes;

        private static readonly string[] DefaultScopes =
        {
            Scopes.Profile
        };

        private static TaskCompletionSource<GoogleResponse> _loginTcs;

        private readonly GoogleSignInClient _mGoogleSignInClient;

        private static Activity CurrentActivity { get; set; }

        /// <summary>
        /// Constructs this object by initializing the <see cref="_mGoogleSignInClient"/>.
        /// </summary>
        /// <exception cref="GoogleClientNotInitializedErrorException">If the current activity is null.</exception>
        public GoogleClientManager()
        {
            if (CurrentActivity == null)
            {
                throw new GoogleClientNotInitializedErrorException(GoogleClientBaseException
                    .ClientNotInitializedErrorMessage);
            }

            var gopBuilder = new GoogleSignInOptions.Builder(GoogleSignInOptions.DefaultSignIn)
                .RequestEmail();

            if (!string.IsNullOrWhiteSpace(_serverClientId))
                gopBuilder.RequestServerAuthCode(_serverClientId);

            if (!string.IsNullOrWhiteSpace(_clientId))
                gopBuilder.RequestIdToken(_clientId);

            foreach (var s in _initScopes)
            {
                gopBuilder.RequestScopes(new Scope(s));
            }

            _mGoogleSignInClient = GoogleSignIn.GetClient(CurrentActivity, gopBuilder.Build());
        }
        
        /// <inheritdoc />
        public GoogleUser CurrentUser
        {
            get
            {
                var userAccount = GoogleSignIn.GetLastSignedInAccount(CurrentActivity);
                return userAccount != null
                    ? new GoogleUser
                    {
                        Id = userAccount.Id,
                        Name = userAccount.DisplayName,
                        GivenName = userAccount.GivenName,
                        FamilyName = userAccount.FamilyName,
                        Email = userAccount.Email,
                        Picture = userAccount.PhotoUrl != null
                            ? new Uri($"{userAccount.PhotoUrl}")
                            : null
                    }
                    : null;
            }
        }

        /// <inheritdoc />
        public bool IsLoggedIn => GoogleSignIn.GetLastSignedInAccount(CurrentActivity) != null;

        /// <summary>
        /// Initializes the google client manager.
        /// </summary>
        /// <param name="activity">The current activity.</param>
        /// <param name="clientId">The client ID, used for requesting ID tokens.</param>
        /// <param name="serverClientId">The server client ID, used for offline access.</param>
        /// <param name="scopes">The scopes that are needed.</param>
        /// <param name="requestCode">The request code for starting the Google sign in activity.</param>
        public static void Initialize(
            Activity activity,
            string clientId = null,
            string serverClientId = null,
            int requestCode = 9637,
            params string[] scopes)
        {
            CurrentActivity = activity;
            _serverClientId = serverClientId;
            _clientId = clientId;
            _requestCode = requestCode;
            _initScopes = DefaultScopes.Concat(scopes).ToArray();
        }

        /// <inheritdoc />
        public async Task<GoogleResponse> LoginAsync()
        {
            return await LoginAsync(false);
        }

        /// <inheritdoc />
        public async Task<GoogleResponse> SilentLoginAsync()
        {
            return await LoginAsync(true);
        }

        /// <summary>
        /// Actually logs a user in. Can be done silently.
        /// </summary>
        /// <param name="silent">If the login attempt must be made silent.</param>
        /// <returns>Response with a google user.</returns>
        private async Task<GoogleResponse> LoginAsync(bool silent)
        {
            _loginTcs = new TaskCompletionSource<GoogleResponse>();

            if (IsLoggedIn && silent)
            {
                //Silent login.
                try
                {
                    var userAccount = await _mGoogleSignInClient.SilentSignInAsync();
                    OnSignInSuccessful(userAccount);
                }
                catch (ApiException e)
                {
                    if (e.StatusCode == CommonStatusCodes.SignInRequired)
                    {
                        StartSignInActivity();
                    }
                    else throw;
                }
            }
            else
            {
                StartSignInActivity();
            }

            return await _loginTcs.Task;
        }

        private void StartSignInActivity()
        {
            var intent = _mGoogleSignInClient.SignInIntent;
            CurrentActivity?.StartActivityForResult(intent, _requestCode);
        }
        
        /// <inheritdoc />
        public void Logout()
        {
            if (!IsLoggedIn) return;

            _mGoogleSignInClient.SignOut();
        }

        /// <inheritdoc />
        public async Task LogoutAsync()
        {
            if (!IsLoggedIn) return;

            await _mGoogleSignInClient.SignOutAsync();
        }
        
        /// <inheritdoc />
        public void RevokeAccess()
        {
            if (!IsLoggedIn) return;

            _mGoogleSignInClient.RevokeAccess();
        }
        
        /// <inheritdoc />
        public async Task RevokeAccessAsync()
        {
            if (!IsLoggedIn) return;

            await _mGoogleSignInClient.RevokeAccessAsync();
        }
        
        /// <summary>
        /// Ran when the authentication has been completed.
        /// </summary>
        /// <param name="requestCode">The request code.</param>
        /// <param name="resultCode">The result code.</param>
        /// <param name="data">The intent with the data.</param>
        public static void OnAuthCompleted(int requestCode, Result resultCode, Intent data)
        {
            if (requestCode != _requestCode)
            {
                return;
            }

            //Get the signed in account and add the oncomplete listener. See OnComplete(). 
            GoogleSignIn.GetSignedInAccountFromIntent(data)
                .AddOnCompleteListener(DependencyService.Get<IGoogleClientManager>() as IOnCompleteListener);
        }

        private void OnSignInSuccessful(GoogleSignInAccount userAccount)
        {
            var googleUser = new GoogleUser
            {
                Id = userAccount.Id,
                Name = userAccount.DisplayName,
                GivenName = userAccount.GivenName,
                FamilyName = userAccount.FamilyName,
                Email = userAccount.Email,
                Picture = userAccount.PhotoUrl != null
                    ? new Uri($"{userAccount.PhotoUrl}")
                    : null,
                IdToken = userAccount.IdToken
            };

            // Send the result to the receivers
            _loginTcs.TrySetResult(new GoogleResponse(googleUser, GoogleActionStatus.Completed));
        }

        private void OnSignInFailed(Exception apiException)
        {
            Android.Util.Log.Error("Error", Throwable.FromException(apiException), "Error occurred");

            // Send the result to the receivers
            _loginTcs.TrySetResult(new GoogleResponse(GoogleActionStatus.Error));
        }

        /// <inheritdoc />
        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (!task.IsSuccessful)
            {
                OnSignInFailed(task.Exception.JavaCast<ApiException>());
            }
            else
            {
                var userAccount = task.Result.JavaCast<GoogleSignInAccount>();

                OnSignInSuccessful(userAccount);
            }
        }
    }
}