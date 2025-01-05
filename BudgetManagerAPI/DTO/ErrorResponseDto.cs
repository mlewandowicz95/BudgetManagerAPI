namespace BudgetManagerAPI.DTO
{
    public class ErrorResponseDto
    {
        public string Message { get; set; } 
        public string ErrorCode { get; set; } 
        public IDictionary<string, string[]> Errors { get; set; } 
        public string TraceId { get; set; } 
    }
}
