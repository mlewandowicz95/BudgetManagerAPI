namespace BudgetManagerAPI.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(int userId, string email, string role);
    }
}
