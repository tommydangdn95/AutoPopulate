namespace Core.Validation
{
    /// <param name="Field">Tên thuộc tính của Passenger (dùng để tô màu ô trên bảng).</param>
    public sealed record ValidationIssue(string Field, string Message, bool IsWarning = false);
}
