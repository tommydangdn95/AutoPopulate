using System.Text.RegularExpressions;
using Core.Abstractions;
using Core.Models;
using Microsoft.Playwright;
using F = Infrastructure.Automation.SiteTexts.Fields;

namespace Infrastructure.Automation
{
    /// <summary>
    /// Điều khiển trình duyệt điền tờ khai trên prearrival.immigration.gov.vn.
    /// Trình duyệt được giữ mở sau khi chạy xong để người dùng làm tiếp các bước còn lại.
    /// </summary>
    public class PrearrivalAutomation(AppSettings settings) : IDeclarationAutomation
    {
        private static readonly TimeSpan UserWaitTimeout = TimeSpan.FromMinutes(15);

        private const int FieldTimeoutMs = 5000;
        /// <summary>Các nút chuyển bước có thể phải chờ site tải dữ liệu nên được chờ lâu hơn.</summary>
        private const int StepTimeoutMs = 15000;
        private const int NavigationTimeoutMs = 30000;
        /// <summary>Số ô liên tiếp không tìm thấy thì thôi điền tiếp khách đó: site nhiều khả năng đã đổi.</summary>
        private const int MaxConsecutiveMissingFields = 3;

        private IPlaywright? _playwright;
        private IBrowserContext? _context;
        private bool _contextClosed;
        private IPage? _page;
        private IAutomationHost _host = null!;
        private CancellationTokenSource? _reviewWatch;

        private IPage Page => _page ?? throw new InvalidOperationException("Trình duyệt chưa được mở.");

        public bool IsBrowserOpen => _context is not null && !_contextClosed;

        private enum Outcome { Filled, Skipped, Stop }

        public async Task RunAsync(GroupSettings group, IReadOnlyList<Passenger> passengers, IAutomationHost host,
            CancellationToken cancellationToken)
        {
            try
            {
                await RunCoreAsync(group, passengers, host, cancellationToken);
            }
            catch (PlaywrightException ex) when (_contextClosed || ex.Message.Contains("has been closed"))
            {
                // Chrome bị đóng giữa chừng: kết thúc bình thường, app được báo qua IAutomationHost.BrowserClosed.
            }
        }

        private async Task RunCoreAsync(GroupSettings group, IReadOnlyList<Passenger> passengers, IAutomationHost host,
            CancellationToken cancellationToken)
        {
            _host = host;
            await OpenBrowserAsync();
            StartReviewWatcher();

            await OpenDeclarationFormAsync(group, cancellationToken);

            if (!await ConfirmFormIsRecognizedAsync(passengers, cancellationToken))
            {
                _host.ReportStatus("Đã dừng. Trình duyệt vẫn mở, bạn có thể làm tiếp bằng tay.");
                return;
            }

            var site = 0; // vị trí "Hành khách thứ #(site+1)" trên form
            var placed = new Dictionary<int, Passenger>();

            for (var i = 0; i < passengers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var passenger = passengers[i];
                await WaitIfCaptchaAsync(cancellationToken);

                if (!await SectionExistsAsync(site) &&
                    !await AddPassengerSectionAsync(site, placed, group, cancellationToken))
                    return;

                _host.ReportStatus($"Đang điền khách {i + 1}/{passengers.Count}: {passenger.FullName} (STT {passenger.No})");
                var outcome = await FillWithReviewAsync(site, passenger, group, cancellationToken);
                if (outcome == Outcome.Stop)
                {
                    _host.ReportStatus("Đã dừng. Trình duyệt vẫn mở, bạn có thể làm tiếp bằng tay.");
                    return;
                }
                if (outcome == Outcome.Filled)
                    placed[site++] = passenger;
            }

            var skipped = passengers.Count(p => p.State == PassengerState.Skipped);
            var message = $"Đã điền xong {placed.Count}/{passengers.Count} khách.";
            if (skipped > 0)
                message += $" {skipped} khách bị bỏ qua (xem cột Thông báo).";
            if (await SectionExistsAsync(site))
                message += $" Xoá ô \"Hành khách thứ #{site + 1}\" còn trống trên trình duyệt trước khi tiếp tục.";
            message += " Kiểm tra lại trên trình duyệt rồi bấm \"Tiếp theo\" để làm các bước còn lại." +
                       " Tới trang Xem lại và nộp, app sẽ tự đối chiếu; nếu không thấy cửa sổ kết quả thì bấm \"Đối chiếu trang Xem lại\" trước khi nộp.";
            _host.ReportStatus(message, requiresUser: true);
        }

        // ------------------------------------------------------------------ trang "Xem lại và nộp"

        public async Task<string?> ReadReviewPageAsync()
        {
            if (_page is not { IsClosed: false } page || _contextClosed) return null;
            try
            {
                if (await page.GetByText(SiteTexts.ReviewPrompt).First.IsVisibleAsync() || await LooksLikeReviewPageAsync(page))
                    return await page.Locator("body").InnerTextAsync();
                return null;
            }
            catch (PlaywrightException)
            {
                return null;
            }
        }

        /// <summary>
        /// Dự phòng khi site đổi câu nhắc của trang Xem lại: có tiêu đề "hành khách thứ #1" nhưng không còn ô nhập
        /// (form nhập cũng có tiêu đề này nhưng còn ô nhập).
        /// </summary>
        private async Task<bool> LooksLikeReviewPageAsync(IPage page)
        {
            var heading = page.GetByText(new Regex(SiteTexts.FirstPassengerHeadingPattern, RegexOptions.IgnoreCase)).First;
            return await heading.IsVisibleAsync()
                   && await page.Locator($"input[name='0_{F.PassportNumber}']").CountAsync() == 0
                   && await page.Locator("input[type='text'], input:not([type])").CountAsync() == 0;
        }

        /// <summary>
        /// Theo dõi Chrome (người dùng tự bấm qua các bước sau khi app điền xong): mỗi lần vào trang
        /// "Xem lại và nộp" thì báo cho app đối chiếu. Chạy tới khi đóng trình duyệt hoặc lần khai mới.
        /// </summary>
        private void StartReviewWatcher()
        {
            _reviewWatch?.Cancel();
            _reviewWatch = new CancellationTokenSource();
            _ = WatchReviewPageAsync(_reviewWatch.Token);
        }

        private async Task WatchReviewPageAsync(CancellationToken ct)
        {
            var onReviewPage = false;
            try
            {
                while (!_contextClosed)
                {
                    await Task.Delay(1500, ct);
                    var text = await ReadReviewPageAsync();
                    if (text is not null && !onReviewPage)
                    {
                        // Chờ trang hiển thị đủ các hành khách rồi đọc lại.
                        await Task.Delay(800, ct);
                        text = await ReadReviewPageAsync();
                        if (text is not null) _host.ReviewPageReached(text);
                    }
                    onReviewPage = text is not null;
                }
            }
            catch (OperationCanceledException)
            {
                // Lần khai mới hoặc đóng trình duyệt.
            }
        }

        public async ValueTask DisposeAsync()
        {
            _reviewWatch?.Cancel();
            if (_context is not null && !_contextClosed)
            {
                try { await _context.CloseAsync(); }
                catch (PlaywrightException) { /* người dùng đã tự đóng trình duyệt */ }
            }
            _playwright?.Dispose();
            _context = null;
            _page = null;
            _playwright = null;
            GC.SuppressFinalize(this);
        }

        // ------------------------------------------------------------------ mở form

        private async Task OpenBrowserAsync()
        {
            if (_page is { IsClosed: false })
            {
                await _page.BringToFrontAsync();
                return;
            }

            // Bản publish single-file: chỉ cho Playwright tìm driver (.playwright\) cạnh AutoPopulate.exe.
            if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, ".playwright")))
                Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", AppContext.BaseDirectory);
            _playwright ??= await Playwright.CreateAsync();
            if (_context is null || _contextClosed)
            {
                // Mở Chrome với profile cố định trong thư mục app (chế độ thường). Context mặc định của
                // Playwright là profile tạm = chế độ ẩn danh: Chrome báo "Anyone using this device can see
                // downloaded files" và file tải về bị đặt tên GUID trong thư mục tạm.
                _host.Log($"Mở trình duyệt ({settings.BrowserChannel})...");
                Directory.CreateDirectory(AppPaths.ChromeProfileFolder);
                try
                {
                    _context = await _playwright.Chromium.LaunchPersistentContextAsync(AppPaths.ChromeProfileFolder,
                        new BrowserTypeLaunchPersistentContextOptions
                        {
                            Headless = false,
                            Channel = settings.BrowserChannel,
                            SlowMo = settings.SlowMoMs,
                            Args = ["--start-maximized"],
                            // Mặc định Playwright thêm --no-sandbox → Chrome hiện cảnh báo "unsupported command-line flag".
                            ChromiumSandbox = true,
                            ViewportSize = ViewportSize.NoViewport,
                            Locale = "vi-VN",
                            AcceptDownloads = true,
                            // Nơi Playwright để file tạm (tên GUID); OnDownload chép sang Data\Downloads bằng tên gốc.
                            DownloadsPath = Path.Combine(AppPaths.DataFolder, "DownloadsTemp")
                        });
                }
                catch (PlaywrightException ex)
                {
                    throw new InvalidOperationException(
                        "Không mở được Chrome. Nếu cửa sổ Chrome của lần chạy trước còn mở, hãy đóng nó rồi thử lại.\n\n" +
                        FirstLine(ex.Message), ex);
                }
                _contextClosed = false;
                _context.Close += (_, _) =>
                {
                    _contextClosed = true;
                    _host.BrowserClosed();
                };
                // Lưu file tải về bằng tên gốc vào Data\Downloads (áp dụng cả tab/popup mới).
                _context.Page += (_, page) => page.Download += OnDownload;
                foreach (var existing in _context.Pages)
                    existing.Download += OnDownload;
            }

            _page = _context.Pages.FirstOrDefault(p => !p.IsClosed) ?? await _context.NewPageAsync();
            // Mặc định cho thao tác trên ô: site bình thường thì điền ngay, chỉ chờ hết khi không tìm thấy ô.
            _page.SetDefaultTimeout(FieldTimeoutMs);
            _page.SetDefaultNavigationTimeout(NavigationTimeoutMs);
            await _page.BringToFrontAsync();
        }

        private async void OnDownload(object? sender, IDownload download)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DownloadsFolder);
                var name = string.Concat(download.SuggestedFilename.Split(Path.GetInvalidFileNameChars()));
                if (string.IsNullOrWhiteSpace(name)) name = $"download_{DateTime.Now:yyyyMMdd_HHmmss}";
                var path = Path.Combine(AppPaths.DownloadsFolder, name);
                for (var i = 2; File.Exists(path); i++)
                    path = Path.Combine(AppPaths.DownloadsFolder, $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}");

                await download.SaveAsAsync(path);
                var failure = await download.FailureAsync();
                if (failure is null)
                    _host.FileDownloaded(path);
                else
                    _host.Log($"Tải file \"{name}\" không thành công: {failure}");
            }
            catch (PlaywrightException ex)
            {
                _host.Log("Không lưu được file tải về: " + FirstLine(ex.Message));
            }
        }

        private async Task OpenDeclarationFormAsync(GroupSettings group, CancellationToken ct)
        {
            _host.Log($"Mở {settings.HomeUrl}");
            await Page.GotoAsync(settings.HomeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            // Bước 1: chọn Tiếng Việt.
            await SelectVietnameseAsync();

            // Bước 2: Tạo và nộp tờ khai.
            await Page.GetByText(SiteTexts.CreateDeclaration).First.ClickAsync(new LocatorClickOptions { Timeout = StepTimeoutMs });

            // Bước 3: CAPTCHA – người dùng tự nhập.
            _host.ReportStatus("Vui lòng nhập CAPTCHA trên trình duyệt rồi bấm \"Xác minh\".", requiresUser: true);
            var nationalityPrompt = Page.GetByText(SiteTexts.ChooseNationality).First;
            var captcha = Page.GetByText(SiteTexts.CaptchaTitle).First;
            await WaitUntilAsync(async () => await nationalityPrompt.IsVisibleAsync() && !await captcha.IsVisibleAsync(),
                UserWaitTimeout, ct, "Quá thời gian chờ nhập CAPTCHA.");

            // Bước 4: chọn quốc tịch cho cả đoàn.
            _host.ReportStatus($"Chọn quốc tịch: {group.Nationality.DisplayName}");
            var nationalityInput = Page.GetByRole(AriaRole.Combobox).First;
            await SelectAutocompleteAsync(nationalityInput, group.Nationality.DisplayName, group.Nationality.SearchText);
            await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = SiteTexts.Next, Exact = true })
                .ClickAsync(new LocatorClickOptions { Timeout = StepTimeoutMs });

            var firstPassport = Input(0, F.PassportNumber);
            await WaitUntilAsync(async () =>
            {
                await WaitIfCaptchaAsync(ct);
                return await firstPassport.CountAsync() > 0;
            }, TimeSpan.FromMinutes(2), ct, "Không mở được form thông tin hành khách.");
        }

        /// <summary>
        /// Rà các ô cần điền ngay khi vào form. Thiếu ô thì báo một lần cho người dùng quyết định,
        /// thay vì để họ chờ lỗi từng ô. Trả về false nếu người dùng chọn dừng.
        /// </summary>
        private async Task<bool> ConfirmFormIsRecognizedAsync(IReadOnlyList<Passenger> passengers, CancellationToken ct)
        {
            var missing = await FindMissingFormFieldsAsync(passengers.Count > 1);
            // Form có thể chưa vẽ xong: thử lại trước khi kết luận.
            for (var attempt = 0; missing.Count > 0 && attempt < 3; attempt++)
            {
                await Task.Delay(1000, ct);
                missing = await FindMissingFormFieldsAsync(passengers.Count > 1);
            }
            if (missing.Count == 0) return true;

            _host.Log("Không thấy các ô trên form: " + string.Join(", ", missing));
            await SaveScreenshotAsync("form-khac-thuong");
            var errors = missing.Select(field => new SiteFieldError(field, "Không thấy trên form")).ToList();
            var decision = await _host.AskUserAsync(new AttentionRequest(passengers[0],
                $"Site có thể đã đổi giao diện – không thấy {missing.Count} ô trên form. " +
                "Bấm Tiếp tục để điền các ô còn lại, hoặc Dừng để khai tay trên trình duyệt", errors, AllowSkip: false), ct);
            return decision != UserDecision.Stop;
        }

        private async Task<List<string>> FindMissingFormFieldsAsync(bool needAddPassenger)
        {
            var checks = new List<(string Name, ILocator Locator)>
            {
                ("Ngày đến", Input(0, F.ArrivalDate)),
                ("Loại hộ chiếu", Input(0, F.PassportType)),
                ("Số hộ chiếu", Input(0, F.PassportNumber)),
                ("Ngày hết hạn hộ chiếu", Input(0, F.PassportExpiry)),
                ("Giới tính", Page.Locator("input[type='radio'][value='male']")),
                ("Họ", Page.GetByPlaceholder(SiteTexts.SurnamePlaceholder)),
                ("Tên", Page.GetByPlaceholder(SiteTexts.GivenNamePlaceholder)),
                ("Ngày sinh", Input(0, F.DateOfBirth)),
                ("Số điện thoại", Input(0, F.Phone)),
                ("Email", Input(0, F.Email)),
                ("Xác nhận đã đọc thông tin visa", Page.Locator("label").Filter(new LocatorFilterOptions { HasText = SiteTexts.VisaAcknowledge })),
                ("Loại thị thực", Input(0, F.VisaType)),
                ("Số thị thực", Input(0, F.VisaNumber)),
                ("Ngày cấp thị thực", Input(0, F.VisaIssueDate)),
                ("Ngày hết hạn thị thực", Input(0, F.VisaExpiry))
            };
            if (needAddPassenger)
                checks.Add(("Nút Thêm hành khách", Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex(SiteTexts.AddPassenger) })));

            var missing = new List<string>();
            foreach (var (name, locator) in checks)
                if (await locator.CountAsync() == 0) missing.Add(name);
            return missing;
        }

        private async Task SelectVietnameseAsync()
        {
            try
            {
                var languageButton = Page.Locator(SiteTexts.LanguageButtonSelector).First;
                await languageButton.ClickAsync(new LocatorClickOptions { Timeout = 10000 });
                await Page.GetByText(SiteTexts.Vietnamese, new PageGetByTextOptions { Exact = true }).First
                    .ClickAsync(new LocatorClickOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                _host.Log("Không tìm thấy nút chọn ngôn ngữ – tiếp tục với ngôn ngữ hiện tại.");
            }

            var create = Page.GetByText(SiteTexts.CreateDeclaration).First;
            try
            {
                await create.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException(
                    $"Không thấy nút \"{SiteTexts.CreateDeclaration}\". Hãy kiểm tra trang đang hiển thị tiếng Việt.");
            }
        }

        // ------------------------------------------------------------------ điền từng khách

        private async Task<Outcome> FillWithReviewAsync(int site, Passenger p, GroupSettings group, CancellationToken ct)
        {
            var fill = true;
            while (true)
            {
                var errors = new List<SiteFieldError>();
                if (fill)
                {
                    p.State = PassengerState.Filling;
                    p.StatusMessage = "";
                    _host.PassengerChanged(p);
                    errors.AddRange(await FillPassengerAsync(site, p, group));
                    // Chờ site kiểm tra hộ chiếu/thị thực (gọi API sau 500ms).
                    await Task.Delay(1500, ct);
                }
                errors.AddRange(await ScanErrorsAsync(site));
                errors = errors.Distinct().ToList();

                if (errors.Count == 0)
                {
                    p.State = PassengerState.Filled;
                    p.StatusMessage = $"Đã điền vào ô Hành khách thứ #{site + 1}";
                    _host.PassengerChanged(p);
                    return Outcome.Filled;
                }

                p.State = PassengerState.NeedsAttention;
                p.StatusMessage = string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"));
                _host.PassengerChanged(p);
                await SaveScreenshotAsync($"stt{p.No}");

                var decision = await _host.AskUserAsync(
                    new AttentionRequest(p, $"Khách #{site + 1} – STT {p.No} – {p.FullName}{p.SourceSuffix}", errors), ct);

                switch (decision)
                {
                    case UserDecision.Continue:
                        fill = false;
                        break;
                    case UserDecision.Refill:
                        fill = true;
                        break;
                    case UserDecision.Skip:
                        p.State = PassengerState.Skipped;
                        p.StatusMessage = $"Bỏ qua – sửa khách STT {p.No} rồi khai lại";
                        _host.PassengerChanged(p);
                        return Outcome.Skipped;
                    default:
                        return Outcome.Stop;
                }
            }
        }

        /// <summary>Điền tất cả các trường; lỗi ở trường nào thì ghi lại và điền tiếp các trường khác.</summary>
        private async Task<List<SiteFieldError>> FillPassengerAsync(int site, Passenger p, GroupSettings group)
        {
            var errors = new List<SiteFieldError>();
            var consecutiveMissing = 0;

            async Task Step(string field, Func<Task> action)
            {
                // Nhiều ô liên tiếp không tìm thấy: site đã đổi, không để người dùng chờ từng ô còn lại.
                if (consecutiveMissing >= MaxConsecutiveMissingFields) return;
                try
                {
                    await action();
                    consecutiveMissing = 0;
                }
                catch (Exception ex) when (ex is PlaywrightException or TimeoutException or FillException)
                {
                    var message = ex is FillException ? ex.Message : "App không điền được trường này: " + FirstLine(ex.Message);
                    errors.Add(new SiteFieldError(field, message));
                    _host.Log($"[STT {p.No}] {field}: {FirstLine(ex.Message)}");

                    consecutiveMissing = ex is TimeoutException ? consecutiveMissing + 1 : 0;
                    if (consecutiveMissing >= MaxConsecutiveMissingFields)
                        errors.Add(new SiteFieldError("Dừng điền khách này",
                            $"{MaxConsecutiveMissingFields} ô liên tiếp không tìm thấy, site có thể đã đổi giao diện. Hãy điền tay trên trình duyệt."));
                }
            }

            await Step("Mở ô hành khách", () => EnsureExpandedAsync(site));
            var section = Section(site);

            if (site == 0)
                await Step("Ngày đến", () => SelectArrivalDateAsync(group.ArrivalDate));

            await Step("Loại hộ chiếu", () => SelectAutocompleteAsync(Input(site, F.PassportType), p.PassportType));
            await Step("Số hộ chiếu", () => FillTextAsync(Input(site, F.PassportNumber), p.PassportNumber));
            await Step("Ngày hết hạn hộ chiếu", () => FillDateAsync(Input(site, F.PassportExpiry), p.PassportExpiry));
            await Step("Giới tính", () => SelectGenderAsync(section, p.Gender));
            await Step("Họ", () => FillTextAsync(section.GetByPlaceholder(SiteTexts.SurnamePlaceholder), p.Surname));
            await Step("Tên", () => FillTextAsync(section.GetByPlaceholder(SiteTexts.GivenNamePlaceholder), p.GivenName));
            await Step("Ngày sinh", () => FillDateAsync(Input(site, F.DateOfBirth), p.DateOfBirth));
            await Step("Số điện thoại", () => FillTextAsync(Input(site, F.Phone), p.Phone));
            await Step("Email", () => FillTextAsync(Input(site, F.Email), group.Email.Trim()));
            if (site > 0)
                await Step("Quan hệ", () => SelectRelationshipIfPresentAsync(site));

            await Step("Xác nhận đã đọc thông tin visa", () => CheckVisaAcknowledgeAsync(section));
            await Step("Loại thị thực", () => SelectAutocompleteAsync(Input(site, F.VisaType), p.VisaType));
            // Chọn Loại thị thực xong site tự điền Ngày cấp: app không điền/xoá ô này, chỉ chờ site điền xong.
            await Step("Ngày cấp thị thực", () => EnsureVisaIssueDateAsync(Input(site, F.VisaIssueDate), p, group.ArrivalDate));
            await Step("Số thị thực", () => FillTextAsync(Input(site, F.VisaNumber), p.VisaNumber));
            await Step("Ngày hết hạn thị thực", () => FillDateAsync(Input(site, F.VisaExpiry), p.VisaExpiry));
            if (!string.IsNullOrWhiteSpace(p.VisaIssuedPlace))
                await Step("Nơi cấp", () => SelectAutocompleteAsync(Input(site, F.VisaIssuedPlace), p.VisaIssuedPlace, exact: false));

            return errors;
        }

        /// <summary>
        /// Thêm ô "Hành khách thứ #n". Nếu site không cho thêm (khách trước còn lỗi), hỏi người dùng sửa rồi thử lại.
        /// </summary>
        private async Task<bool> AddPassengerSectionAsync(int site, Dictionary<int, Passenger> placed, GroupSettings group,
            CancellationToken ct)
        {
            while (true)
            {
                await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex(SiteTexts.AddPassenger) })
                    .ClickAsync(new LocatorClickOptions { Timeout = StepTimeoutMs });
                try
                {
                    await Input(site, F.PassportNumber).WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = 5000
                    });
                    return true;
                }
                catch (TimeoutException)
                {
                    // Site không thêm ô mới: tìm khách đang có lỗi.
                }

                await WaitIfCaptchaAsync(ct);

                var (index, errors) = (-1, new List<SiteFieldError>());
                foreach (var (s, _) in placed.OrderBy(kv => kv.Key))
                {
                    var found = await ScanErrorsAsync(s);
                    if (found.Count > 0)
                    {
                        (index, errors) = (s, found);
                        break;
                    }
                }

                var passenger = index >= 0 ? placed[index] : placed[site - 1];
                if (errors.Count == 0)
                    errors.Add(new SiteFieldError("Thêm hành khách", "Site không thêm được ô hành khách mới. Kiểm tra trên trình duyệt."));

                var previousState = passenger.State;
                passenger.State = PassengerState.NeedsAttention;
                passenger.StatusMessage = string.Join("; ", errors.Select(e => $"{e.Field}: {e.Message}"));
                _host.PassengerChanged(passenger);
                await SaveScreenshotAsync($"stt{passenger.No}");

                var decision = await _host.AskUserAsync(new AttentionRequest(passenger,
                    $"Không thêm được khách mới – khách #{index + 1} (STT {passenger.No}) đang có lỗi",
                    errors, AllowSkip: false), ct);

                if (decision == UserDecision.Stop)
                {
                    _host.ReportStatus("Đã dừng. Trình duyệt vẫn mở, bạn có thể làm tiếp bằng tay.");
                    return false;
                }
                if (decision == UserDecision.Refill && index >= 0)
                    await FillPassengerAsync(index, passenger, group);

                passenger.State = previousState == PassengerState.NeedsAttention ? PassengerState.Filled : previousState;
                passenger.StatusMessage = "";
                _host.PassengerChanged(passenger);
            }
        }

        // ------------------------------------------------------------------ thao tác trên trường

        private ILocator Input(int site, string field) => Page.Locator($"input[name='{site}_{field}']");

        /// <summary>Khối chứa toàn bộ trường của một hành khách (từ Số hộ chiếu đến phần Visa).</summary>
        private ILocator Section(int site) => Page.Locator(
            $"xpath=//input[@name='{site}_{F.PassportNumber}']/ancestor::div[.//input[@name='{site}_{F.VisaNumber}']][1]");

        private async Task<bool> SectionExistsAsync(int site) => await Input(site, F.PassportNumber).CountAsync() > 0;

        private async Task EnsureExpandedAsync(int site)
        {
            var passport = Input(site, F.PassportNumber);
            if (await passport.IsVisibleAsync()) return;

            var header = Page.GetByText(new Regex(string.Format(SiteTexts.PassengerHeaderPattern, site + 1), RegexOptions.IgnoreCase)).First;
            await header.ClickAsync();
            await passport.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }

        private async Task SelectArrivalDateAsync(DateTime date)
        {
            var label = date.ToString("dd/MM/yyyy");
            var options = Page.Locator($"xpath=//input[@name='0_{F.ArrivalDate}']/ancestor::button[1]");
            var texts = (await options.AllInnerTextsAsync()).Select(t => t.Trim()).ToList();
            var index = texts.FindIndex(t => t.Contains(label));
            if (index < 0)
                throw new FillException($"Trang không có lựa chọn ngày {label}. Các ngày đang có: {string.Join(", ", texts)}.");
            await options.Nth(index).ClickAsync();
        }

        private async Task FillTextAsync(ILocator input, string value)
        {
            await input.FillAsync(value);
            await input.PressAsync("Tab");
        }

        /// <summary>Ô ngày của MUI DatePicker: gõ từng chữ số DDMMYYYY; null thì xoá trống.</summary>
        private async Task FillDateAsync(ILocator input, DateTime? date)
        {
            await input.ClickAsync();
            await input.PressAsync("Control+A");
            await input.PressAsync("Backspace");
            if (date is null)
            {
                await input.PressAsync("Tab");
                return;
            }

            var expected = date.Value.ToString("dd/MM/yyyy");
            await input.PressSequentiallyAsync(date.Value.ToString("ddMMyyyy"), new LocatorPressSequentiallyOptions { Delay = 40 });
            await input.PressAsync("Tab");

            var actual = (await input.InputValueAsync()).Trim();
            if (actual == expected) return;

            // Cách dự phòng: điền thẳng chuỗi có dấu "/".
            await input.FillAsync(expected);
            await input.PressAsync("Tab");
            actual = (await input.InputValueAsync()).Trim();
            if (actual != expected)
                throw new FillException($"Không nhập được ngày {expected} (ô đang hiển thị \"{actual}\").");
        }

        /// <summary>
        /// Ngày cấp thị thực bắt buộc phải có. Chọn Loại thị thực xong site thường tự điền ô này:
        /// chờ tối đa 5 giây; site tự điền thì giữ nguyên, không thì điền bằng Ngày đến dự kiến.
        /// </summary>
        private async Task EnsureVisaIssueDateAsync(ILocator input, Passenger p, DateTime arrivalDate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                var value = (await input.InputValueAsync()).Trim();
                if (DateTime.TryParseExact(value, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out _))
                {
                    _host.Log($"[STT {p.No}] Site tự điền Ngày cấp thị thực: {value}");
                    return;
                }
                await Task.Delay(250);
            }

            _host.Log($"[STT {p.No}] Site không tự điền Ngày cấp thị thực sau 5 giây – điền bằng Ngày đến dự kiến {arrivalDate:dd/MM/yyyy}.");
            await FillDateAsync(input, arrivalDate);
        }

        private async Task SelectAutocompleteAsync(ILocator input, string optionText, string? search = null, bool exact = true)
        {
            if ((await input.InputValueAsync()).Trim().Equals(optionText, StringComparison.OrdinalIgnoreCase))
                return;

            await input.ClickAsync();
            await input.FillAsync(search ?? optionText);

            var options = Page.Locator("[role='listbox'] [role='option']");
            var option = exact
                ? options.Filter(new LocatorFilterOptions { HasTextRegex = new Regex("^\\s*" + Regex.Escape(optionText) + "\\s*$") })
                : options.Filter(new LocatorFilterOptions { HasText = optionText });
            try
            {
                await option.First.ClickAsync(new LocatorClickOptions { Timeout = 8000 });
            }
            catch (TimeoutException)
            {
                var available = await options.AllInnerTextsAsync();
                await input.PressAsync("Escape");
                throw new FillException($"Không có lựa chọn \"{optionText}\". Các lựa chọn: {string.Join(" | ", available.Take(15))}");
            }
        }

        /// <summary>
        /// Ô "Mối quan hệ với người khai" (bắt buộc từ khách thứ 2 trở đi): luôn chọn "Đồng nghiệp".
        /// Không có ô này thì bỏ qua.
        /// </summary>
        private async Task SelectRelationshipIfPresentAsync(int site)
        {
            var input = Input(site, F.Relationship);
            if (await input.CountAsync() == 0) return;
            await SelectAutocompleteAsync(input, SiteTexts.DefaultRelationship);
        }

        private async Task SelectGenderAsync(ILocator section, Gender gender)
        {
            if (gender == Gender.Unknown)
                throw new FillException("Chưa có giới tính.");
            var radio = Page.Locator($"input[type='radio'][value='{SiteTexts.GenderValue(gender)}']");
            await section.Locator("label").Filter(new LocatorFilterOptions { Has = radio }).First.ClickAsync();
        }

        private async Task CheckVisaAcknowledgeAsync(ILocator section)
        {
            var label = section.Locator("label").Filter(new LocatorFilterOptions { HasText = SiteTexts.VisaAcknowledge }).First;
            if (!await label.Locator("input[type='checkbox']").IsCheckedAsync())
                await label.ClickAsync();
        }

        // ------------------------------------------------------------------ kiểm tra lỗi trên form

        /// <summary>
        /// Đọc các thông báo lỗi "❗ ..." trong khối của một hành khách, kèm nhãn của trường.
        /// </summary>
        private async Task<List<SiteFieldError>> ScanErrorsAsync(int site)
        {
            var section = Section(site);
            if (await section.CountAsync() == 0) return [];

            const string script = """
                (root) => {
                  const starts = el => (el.textContent || '').trim().startsWith('❗');
                  const out = [];
                  for (const el of root.querySelectorAll('*')) {
                    if (!starts(el) || Array.from(el.children).some(starts)) continue;
                    let label = '';
                    for (let n = el.parentElement, k = 0; n && k < 4 && !label; n = n.parentElement, k++) {
                      const l = n.querySelector('label, legend');
                      if (l) label = l.textContent.replace('*', '').trim();
                    }
                    out.push(label + '|' + el.textContent.replace('❗', '').trim());
                  }
                  return out;
                }
                """;
            var items = await section.First.EvaluateAsync<string[]>(script);
            return items
                .Select(i => i.Split('|', 2))
                .Select(parts => new SiteFieldError(parts[0] == "" ? "(không rõ trường)" : parts[0], parts[1]))
                .Distinct()
                .ToList();
        }

        private async Task WaitIfCaptchaAsync(CancellationToken ct)
        {
            var captcha = Page.GetByText(SiteTexts.CaptchaTitle).First;
            if (!await captcha.IsVisibleAsync()) return;

            _host.ReportStatus("Site yêu cầu CAPTCHA – vui lòng nhập trên trình duyệt.", requiresUser: true);
            await WaitUntilAsync(async () => !await captcha.IsVisibleAsync(), UserWaitTimeout, ct,
                "Quá thời gian chờ nhập CAPTCHA.");
            _host.ReportStatus("Đã qua CAPTCHA, tiếp tục...");
        }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken ct, string timeoutMessage)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!await condition())
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException(timeoutMessage);
                await Task.Delay(500, ct);
            }
        }

        private async Task SaveScreenshotAsync(string name)
        {
            try
            {
                var folder = AppPaths.ScreenshotFolder;
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd_HHmmss}_{name}.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true, Timeout = StepTimeoutMs });
                _host.Log($"Đã chụp màn hình: {path}");
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
                _host.Log("Không chụp được màn hình: " + FirstLine(ex.Message));
            }
        }

        private static string FirstLine(string message) =>
            message.Split('\n', 2)[0].Trim();

        private sealed class FillException(string message) : Exception(message);
    }
}
