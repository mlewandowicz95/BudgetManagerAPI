namespace BudgetManagerAPI.DTO
{
    public class SuccessResponseDto<T> : BaseResponseDto
    {
        public T Data { get; set; }
    }
}
