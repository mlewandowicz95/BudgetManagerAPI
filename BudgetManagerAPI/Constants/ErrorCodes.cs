namespace BudgetManagerAPI.Constants
{
    public static class ErrorCodes
    {
        // Walidacja
        public const string ValidationError = "VALIDATION_ERROR";

        // Logowanie i rejestracja
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string AccountNotActivated = "ACCOUNT_NOT_ACTIVATED";
        public const string UserAlreadyExists = "USER_ALREADY_EXISTS";
        public const string UserAlreadyActive = "USER_ALREADY_ACTIVE";
        public const string UserNotFound = "USER_NOT_FOUND";
        public const string PasswordsMismatch = "PASSWORDS_MISMATCH";

        // Tokeny aktywacyjne
        public const string MissingToken = "MISSING_TOKEN";
        public const string InvalidToken = "INVALID_TOKEN";
        public const string ExpiredToken = "EXPIRED_TOKEN";

        // Serwer
        public const string InternalServerError = "INTERNAL_SERVER_ERROR";

        // Autoryzacja
        public const string InvalidAuthorizationHeader = "INVALID_AUTHORIZATION_HEADER";
        public const string Unathorized = "UNATHORIZED";
    }
}
