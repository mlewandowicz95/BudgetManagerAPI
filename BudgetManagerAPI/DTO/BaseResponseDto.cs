namespace BudgetManagerAPI.DTO
{
    public class BaseResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string TraceId { get; set; }
    }
}
