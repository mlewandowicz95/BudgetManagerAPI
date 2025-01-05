namespace BudgetManagerAPI.DTO
{
    public class ErrorResponseDto : BaseResponseDto
    {
        public string ErrorCode { get; set; } 
        public IDictionary<string, string[]> Errors { get; set; } 
    }
}
