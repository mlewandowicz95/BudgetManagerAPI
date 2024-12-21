namespace BudgetManagerAPI.Constants
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Pro = "Pro";
        public const string User = "User";

        public static readonly List<string> All = new List<string> { Admin, Pro, User };
    }
}
