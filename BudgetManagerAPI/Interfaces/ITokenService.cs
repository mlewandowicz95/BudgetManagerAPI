﻿namespace BudgetManagerAPI.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(int userId, string role, string email);
    }
}
