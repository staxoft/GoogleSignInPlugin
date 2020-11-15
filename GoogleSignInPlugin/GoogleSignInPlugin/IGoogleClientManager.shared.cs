using System;
using System.Threading.Tasks;

namespace E_Token.GoogleSignInPlugin
{
    public enum GoogleClientErrorType
    {
        SignInUnknownError,
        SignInKeychainError,
        NoSignInHandlersInstalledError,
        SignInHasNoAuthInKeychainError,
        SignInCanceledError,
        SignInDefaultError,
        SignInApiNotConnectedError,
        SignInInvalidAccountError,
        SignInNetworkError,
        SignInInternalError,
        SignInRequiredError,
        SignInFailedError
    }

    public enum GoogleActionStatus
    {
        Canceled,
        Unauthorized,
        Completed,
        Error
    }

    public class GoogleResponse
    {
        public GoogleResponse(GoogleUser user, GoogleActionStatus status)
        {
            User = user;
            Status = status;
        }
        
        public GoogleResponse(GoogleActionStatus status)
        {
            Status = status;
        }

        public GoogleUser User { get; }
        public GoogleActionStatus Status { get; set; }
    }

    /// <summary>
    /// Interface for GoogleClientManager
    /// </summary>
    public interface IGoogleClientManager
    {
        /// <summary>
        /// Gets the current logged in user.
        /// </summary>
        GoogleUser CurrentUser { get; }
        
        /// <summary>
        /// Indicates if a user is currently logged.
        /// </summary>
        bool IsLoggedIn { get; }
        
        /// <summary>
        /// Logs the user in.
        /// </summary>
        /// <returns>A <see cref="GoogleResponse"/>.</returns>
        Task<GoogleResponse> LoginAsync();
        
        /// <summary>
        /// Logs the user silently in.
        /// </summary>
        /// <returns>A <see cref="GoogleResponse"/>.</returns>
        Task<GoogleResponse> SilentLoginAsync();
        
        /// <summary>
        /// Logs the user out.
        /// </summary>
        void Logout();

        /// <summary>
        /// Logs the user out async.
        /// </summary>
        Task LogoutAsync();

        /// <summary>
        /// Revoke the access of the user.
        /// </summary>
        void RevokeAccess();
        
        /// <summary>
        /// Revoke the access of the user async.
        /// </summary>
        Task RevokeAccessAsync();
    }
}