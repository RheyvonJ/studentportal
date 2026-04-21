namespace StudentPortal.Models.Library
{
    /// <summary>
    /// SLSHS Library eBook row shown alongside class learning materials.
    /// </summary>
    public class MaterialLinkedEbookDisplay
    {
        public string BookId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Subject { get; set; } = "";
    }
}
