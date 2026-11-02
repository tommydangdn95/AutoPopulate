namespace Infrastructure.Automation
{
    /// <summary>
    /// Chuỗi hiển thị và tên trường của site (giao diện tiếng Việt).
    /// Khi site đổi giao diện, chỉ cần sửa ở đây.
    /// </summary>
    internal static class SiteTexts
    {
        /// <summary>Nút ngôn ngữ trên header: ảnh cờ + mã ngôn ngữ (vd "en").</summary>
        public const string LanguageButtonSelector = "header button:has(img[src*='/flags/'])";
        public const string Vietnamese = "Tiếng Việt";
        public const string CreateDeclaration = "Tạo và nộp tờ khai";
        public const string CaptchaTitle = "Xác minh CAPTCHA";
        public const string ChooseNationality = "Chọn Quốc tịch của bạn";
        public const string Next = "Tiếp theo";
        public const string AddPassenger = "Thêm hành khách";
        public const string PassengerHeaderPattern = "khách thứ #{0}(?!\\d)";
        public const string SurnamePlaceholder = "Nhập họ của bạn";
        public const string GivenNamePlaceholder = "Nhập tên của bạn";
        public const string VisaAcknowledge = "Tôi đã đọc";

        /// <summary>Lựa chọn mặc định cho ô "Mối quan hệ với người khai" (khách thứ 2 trở đi).</summary>
        public const string DefaultRelationship = Core.Models.Catalog.DefaultRelationship;

        /// <summary>Dòng chỉ có ở trang "Xem lại và nộp" (dòng "Xem lại và nộp" của thanh bước thì trang nào cũng có).</summary>
        public const string ReviewPrompt = "Vui lòng xác nhận thông tin bên dưới";

        /// <summary>Hậu tố thuộc tính name của ô nhập: name="{index}_{field}".</summary>
        public static class Fields
        {
            public const string ArrivalDate = "arrivalDate";
            public const string PassportType = "passportType";
            public const string PassportNumber = "passportNumber";
            public const string PassportExpiry = "expiryDate";
            public const string DateOfBirth = "dob";
            public const string Phone = "phone";
            public const string Email = "email";
            public const string Relationship = "relationship";
            public const string VisaType = "visaType";
            public const string VisaNumber = "visaNumber";
            public const string VisaIssueDate = "visaIssueDate";
            public const string VisaExpiry = "visaExpiryDate";
            public const string VisaIssuedPlace = "visaIssuedPlace";
        }

        public static string GenderValue(Core.Models.Gender gender) => gender switch
        {
            Core.Models.Gender.Male => "male",
            Core.Models.Gender.Female => "female",
            _ => "other"
        };
    }
}
