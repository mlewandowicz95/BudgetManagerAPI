﻿namespace BudgetManagerAPI.DTO
{
    public class GoalResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public decimal TargetAmount { get; set; }
        public decimal CurrentProgress { get; set; } = 0m;
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
