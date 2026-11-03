namespace MediaMasterApi.Models
{
    public class MediaModel
    {
        public int MediaId { get; set; }

        public string MediaName { get; set; } = "";

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = "";

        public string MediaType { get; set; } = "";

        public string MediaUrl { get; set; } = "";

        public string PublicId { get; set; } = "";

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }

    public class MediaStatusRequest
    {
        public bool IsActive { get; set; }
    }
}