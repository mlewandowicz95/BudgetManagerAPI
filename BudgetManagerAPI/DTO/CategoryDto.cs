namespace BudgetManagerAPI.DTO
{
    public class CategoryRequestDto
    {
        public string Name { get; set; }
        public int? UserId { get; set; } = null;
    }

    public class CategoryResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? UserId { get; set; }
    }

}
