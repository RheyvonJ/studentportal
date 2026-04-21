using System.Net;

namespace StudentPortal.Services
{
    /// <summary>
    /// HTML email for “join class code” notifications (Sta. Lucia SHS LMS layout).
    /// </summary>
    public static class JoinClassEmailTemplate
    {
        public const string DefaultSubject = "Join Class Code";

        /// <param name="logoAbsoluteUrl">Optional full URL to school logo image (many clients block remote images until allowed).</param>
        public static string BuildHtml(
            string studentDisplayName,
            string courseName,
            string classCode,
            string schoolName,
            string? logoAbsoluteUrl,
            string? classAbsoluteUrl)
        {
            static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            var safeName = E(studentDisplayName);
            var safeCourse = E(courseName);
            var safeSchool = E(string.IsNullOrWhiteSpace(schoolName) ? "Sta. Lucia Senior High School" : schoolName);
            var code = classCode ?? string.Empty;
            var spacedCode = string.Join("&nbsp;", code.ToCharArray().Select(c => E(c.ToString())));
            var safeClassUrl = string.IsNullOrWhiteSpace(classAbsoluteUrl) ? string.Empty : E(classAbsoluteUrl);

            const string navy = "#002147";
            const string subBanner = "#F0F4F8";
            const string subBannerBorder = "#cfd8e3";
            const string bodyText = "#0b213a";
            const string muted = "#5f6f82";

            var logoBlock = !string.IsNullOrWhiteSpace(logoAbsoluteUrl)
                ? $@"<img src=""{E(logoAbsoluteUrl)}"" alt=""{safeSchool} logo"" width=""56"" height=""56"" style=""display:block;margin:0 auto 10px;border-radius:50%;background:#ffffff;padding:6px;object-fit:contain;box-sizing:border-box;"" />"
                : $@"<div style=""width:56px;height:56px;margin:0 auto 10px;border-radius:50%;background:rgba(255,255,255,0.12);border:1px solid rgba(255,255,255,0.25);display:flex;align-items:center;justify-content:center;font-size:10px;color:rgba(255,255,255,0.85);text-align:center;line-height:1.2;padding:6px;box-sizing:border-box;"">Logo</div>
                    <div style=""font-size:11px;color:rgba(255,255,255,0.75);margin-bottom:8px;"">Sta. Lucia SHS</div>";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/><title>Join Class Code</title></head>
<body style=""margin:0;padding:24px 12px;background:#e8ecf1;font-family:Segoe UI,Helvetica Neue,Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;width:100%;border-collapse:collapse;border-radius:16px;overflow:hidden;box-shadow:0 12px 40px rgba(0,33,71,0.12);"">
          <tr>
            <td style=""background:{navy};padding:28px 24px 26px;text-align:center;"">
              {logoBlock}
              <div style=""font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.02em;line-height:1.2;"">{safeSchool}</div>
              <div style=""margin-top:8px;font-size:11px;font-weight:600;color:#a8bdd9;letter-spacing:0.14em;text-transform:uppercase;"">Learning Management System</div>
            </td>
          </tr>
          <tr>
            <td style=""background:{subBanner};padding:22px 28px 18px;border-bottom:1px solid {subBannerBorder};"">
              <div style=""font-size:20px;font-weight:800;color:{navy};letter-spacing:-0.02em;line-height:1.2;"">Join Class Code</div>
              <div style=""margin-top:8px;font-size:14px;color:{bodyText};line-height:1.45;"">Use the code below to join your class in the student portal.</div>
            </td>
          </tr>
          <tr>
            <td style=""background:#ffffff;padding:28px 28px 24px;color:{bodyText};"">
              <p style=""margin:0 0 14px;font-size:15px;line-height:1.5;"">Greetings, <strong style=""color:{navy};"">{safeName}</strong>,</p>
              <p style=""margin:0 0 22px;font-size:15px;line-height:1.5;"">Here is the join class code for the course <strong style=""color:{navy};"">{safeCourse}</strong>:</p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;"">
                <tr>
                  <td style=""background:{navy};border-radius:12px;padding:22px 20px;text-align:center;"">
                    <span style=""font-size:28px;font-weight:800;color:#ffffff;letter-spacing:0.35em;line-height:1.3;"">{spacedCode}</span>
                  </td>
                </tr>
              </table>
              {(string.IsNullOrWhiteSpace(safeClassUrl) ? string.Empty : $@"
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin-top:18px;"">
                <tr>
                  <td align=""center"">
                    <a href=""{safeClassUrl}"" style=""display:inline-block;padding:12px 22px;background:{navy};color:#ffffff;font-size:14px;font-weight:800;text-decoration:none;border-radius:10px;letter-spacing:0.02em;"">Open class</a>
                    <div style=""margin-top:10px;font-size:12px;color:{muted};line-height:1.45;word-break:break-all;"">{safeClassUrl}</div>
                  </td>
                </tr>
              </table>
              ")}
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin-top:22px;"">
                <tr>
                  <td style=""width:5px;background:{navy};border-radius:4px 0 0 4px;"">&nbsp;</td>
                  <td style=""background:#f4f6f9;padding:16px 18px;border-radius:0 8px 8px 0;border:1px solid rgba(0,33,71,0.08);border-left:none;"">
                    <p style=""margin:0;font-size:14px;color:{muted};line-height:1.55;"">Please use this code to join the class in your student portal. If you encounter any issues, feel free to reach out for assistance. We look forward to your active participation in the course.</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style=""background:{navy};padding:20px 24px;text-align:center;"">
              <div style=""font-size:13px;font-weight:700;color:#ffffff;"">Thank you.</div>
              <div style=""margin-top:10px;font-size:11px;color:#8fa8c4;line-height:1.4;"">{safeSchool} &mdash; Learning Management System</div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        public const string OnlineMeetingDefaultSubjectPrefix = "Online class meeting";

        /// <summary>Same visual shell as <see cref="BuildHtml"/> for “teacher created a meet” notifications.</summary>
        public static string BuildOnlineMeetingHtml(
            string studentDisplayName,
            string courseName,
            string classCode,
            string meetingTitle,
            string? scheduledAtDisplay,
            string joinUrl,
            string? portalJoinAbsoluteUrl,
            string schoolName,
            string? logoAbsoluteUrl)
        {
            static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            var safeName = E(studentDisplayName);
            var safeCourse = E(courseName);
            var safeClassCode = E(classCode);
            var safeTitle = E(meetingTitle);
            var safeSchool = E(string.IsNullOrWhiteSpace(schoolName) ? "Sta. Lucia Senior High School" : schoolName);
            var safeJoinHref = E(joinUrl);
            var scheduleBlock = string.IsNullOrWhiteSpace(scheduledAtDisplay)
                ? string.Empty
                : $@"<p style=""margin:0 0 18px;font-size:15px;line-height:1.5;""><strong style=""color:#002147;"">Schedule:</strong> {E(scheduledAtDisplay)}</p>";

            var portalLine = string.IsNullOrWhiteSpace(portalJoinAbsoluteUrl)
                ? "You can also open your <strong style=\"color:#002147;\">student portal</strong>, go to this class, and use the <strong>Join meet</strong> card."
                : $@"You can also join from your student portal: <a href=""{E(portalJoinAbsoluteUrl)}"" style=""color:#1f4f84;font-weight:700;"">Open class &amp; Join meet</a>.";

            const string navy = "#002147";
            const string subBanner = "#F0F4F8";
            const string subBannerBorder = "#cfd8e3";
            const string bodyText = "#0b213a";
            const string muted = "#5f6f82";

            var logoBlock = !string.IsNullOrWhiteSpace(logoAbsoluteUrl)
                ? $@"<img src=""{E(logoAbsoluteUrl)}"" alt=""{safeSchool} logo"" width=""56"" height=""56"" style=""display:block;margin:0 auto 10px;border-radius:50%;background:#ffffff;padding:6px;object-fit:contain;box-sizing:border-box;"" />"
                : $@"<div style=""width:56px;height:56px;margin:0 auto 10px;border-radius:50%;background:rgba(255,255,255,0.12);border:1px solid rgba(255,255,255,0.25);display:flex;align-items:center;justify-content:center;font-size:10px;color:rgba(255,255,255,0.85);text-align:center;line-height:1.2;padding:6px;box-sizing:border-box;"">Logo</div>
                    <div style=""font-size:11px;color:rgba(255,255,255,0.75);margin-bottom:8px;"">Sta. Lucia SHS</div>";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/><title>Online class meeting</title></head>
<body style=""margin:0;padding:24px 12px;background:#e8ecf1;font-family:Segoe UI,Helvetica Neue,Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;width:100%;border-collapse:collapse;border-radius:16px;overflow:hidden;box-shadow:0 12px 40px rgba(0,33,71,0.12);"">
          <tr>
            <td style=""background:{navy};padding:28px 24px 26px;text-align:center;"">
              {logoBlock}
              <div style=""font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.02em;line-height:1.2;"">{safeSchool}</div>
              <div style=""margin-top:8px;font-size:11px;font-weight:600;color:#a8bdd9;letter-spacing:0.14em;text-transform:uppercase;"">Learning Management System</div>
            </td>
          </tr>
          <tr>
            <td style=""background:{subBanner};padding:22px 28px 18px;border-bottom:1px solid {subBannerBorder};"">
              <div style=""font-size:20px;font-weight:800;color:{navy};letter-spacing:-0.02em;line-height:1.2;"">Online Class Meeting</div>
              <div style=""margin-top:8px;font-size:14px;color:{bodyText};line-height:1.45;"">Your instructor has scheduled an online meeting. Use the button below to join at the appropriate time.</div>
            </td>
          </tr>
          <tr>
            <td style=""background:#ffffff;padding:28px 28px 24px;color:{bodyText};"">
              <p style=""margin:0 0 14px;font-size:15px;line-height:1.5;"">Greetings, <strong style=""color:{navy};"">{safeName}</strong>,</p>
              <p style=""margin:0 0 10px;font-size:15px;line-height:1.5;"">A new online class meeting has been created for <strong style=""color:{navy};"">{safeCourse}</strong> <span style=""color:{muted};font-weight:600;"">({safeClassCode})</span>.</p>
              <p style=""margin:0 0 18px;font-size:15px;line-height:1.5;""><strong style=""color:{navy};"">Title:</strong> {safeTitle}</p>
              {scheduleBlock}
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;"">
                <tr>
                  <td style=""background:{navy};border-radius:12px;padding:20px 20px;text-align:center;"">
                    <a href=""{safeJoinHref}"" style=""display:inline-block;padding:14px 28px;background:#ffffff;color:{navy};font-size:16px;font-weight:800;text-decoration:none;border-radius:10px;letter-spacing:0.02em;"">Join meeting</a>
                    <div style=""margin-top:14px;font-size:12px;color:rgba(255,255,255,0.88);word-break:break-all;line-height:1.45;"">{safeJoinHref}</div>
                  </td>
                </tr>
              </table>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin-top:22px;"">
                <tr>
                  <td style=""width:5px;background:{navy};border-radius:4px 0 0 4px;"">&nbsp;</td>
                  <td style=""background:#f4f6f9;padding:16px 18px;border-radius:0 8px 8px 0;border:1px solid rgba(0,33,71,0.08);border-left:none;"">
                    <p style=""margin:0;font-size:14px;color:{muted};line-height:1.55;"">{portalLine} If you need help, contact your instructor or school IT support.</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style=""background:{navy};padding:20px 24px;text-align:center;"">
              <div style=""font-size:13px;font-weight:700;color:#ffffff;"">Thank you.</div>
              <div style=""margin-top:10px;font-size:11px;color:#8fa8c4;line-height:1.4;"">{safeSchool} &mdash; Learning Management System</div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        public const string NewUploadDefaultSubjectPrefix = "New upload";
        public const string NewAnnouncementDefaultSubjectPrefix = "New announcement";

        public static string BuildNewUploadHtml(
            string studentDisplayName,
            string courseName,
            string classCode,
            string contentTypeLabel,
            string contentTitle,
            string schoolName,
            string? logoAbsoluteUrl)
        {
            static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            const string navy = "#002147";
            const string subBanner = "#F0F4F8";
            const string subBannerBorder = "#cfd8e3";
            const string bodyText = "#0b213a";
            const string muted = "#5f6f82";

            var safeName = E(studentDisplayName);
            var safeCourse = E(courseName);
            var safeCode = E(classCode);
            var safeType = E(string.IsNullOrWhiteSpace(contentTypeLabel) ? "content" : contentTypeLabel);
            var safeTitle = E(string.IsNullOrWhiteSpace(contentTitle) ? "(Untitled)" : contentTitle);
            var safeSchool = E(string.IsNullOrWhiteSpace(schoolName) ? "Sta. Lucia Senior High School" : schoolName);

            var logoBlock = !string.IsNullOrWhiteSpace(logoAbsoluteUrl)
                ? $@"<img src=""{E(logoAbsoluteUrl)}"" alt=""{safeSchool} logo"" width=""56"" height=""56"" style=""display:block;margin:0 auto 10px;border-radius:50%;background:#ffffff;padding:6px;object-fit:contain;box-sizing:border-box;"" />"
                : $@"<div style=""width:56px;height:56px;margin:0 auto 10px;border-radius:50%;background:rgba(255,255,255,0.12);border:1px solid rgba(255,255,255,0.25);display:flex;align-items:center;justify-content:center;font-size:10px;color:rgba(255,255,255,0.85);text-align:center;line-height:1.2;padding:6px;box-sizing:border-box;"">Logo</div>
                    <div style=""font-size:11px;color:rgba(255,255,255,0.75);margin-bottom:8px;"">Sta. Lucia SHS</div>";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/><title>New upload</title></head>
<body style=""margin:0;padding:24px 12px;background:#e8ecf1;font-family:Segoe UI,Helvetica Neue,Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;width:100%;border-collapse:collapse;border-radius:16px;overflow:hidden;box-shadow:0 12px 40px rgba(0,33,71,0.12);"">
          <tr>
            <td style=""background:{navy};padding:28px 24px 26px;text-align:center;"">
              {logoBlock}
              <div style=""font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.02em;line-height:1.2;"">{safeSchool}</div>
              <div style=""margin-top:8px;font-size:11px;font-weight:600;color:#a8bdd9;letter-spacing:0.14em;text-transform:uppercase;"">Learning Management System</div>
            </td>
          </tr>
          <tr>
            <td style=""background:{subBanner};padding:22px 28px 18px;border-bottom:1px solid {subBannerBorder};"">
              <div style=""font-size:20px;font-weight:800;color:{navy};letter-spacing:-0.02em;line-height:1.2;"">New Upload</div>
              <div style=""margin-top:8px;font-size:14px;color:{bodyText};line-height:1.45;"">Your instructor uploaded new classwork.</div>
            </td>
          </tr>
          <tr>
            <td style=""background:#ffffff;padding:28px 28px 24px;color:{bodyText};"">
              <p style=""margin:0 0 14px;font-size:15px;line-height:1.5;"">Greetings, <strong style=""color:{navy};"">{safeName}</strong>,</p>
              <p style=""margin:0 0 10px;font-size:15px;line-height:1.5;"">A new <strong style=""color:{navy};"">{safeType}</strong> was uploaded in <strong style=""color:{navy};"">{safeCourse}</strong> <span style=""color:{muted};font-weight:600;"">({safeCode})</span>.</p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin-top:16px;"">
                <tr>
                  <td style=""background:#f4f6f9;border:1px solid rgba(0,33,71,0.08);border-radius:12px;padding:18px 18px;"">
                    <div style=""font-size:12px;color:{muted};font-weight:700;letter-spacing:0.12em;text-transform:uppercase;"">Title</div>
                    <div style=""margin-top:8px;font-size:18px;font-weight:800;color:{navy};line-height:1.25;"">{safeTitle}</div>
                  </td>
                </tr>
              </table>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin-top:18px;"">
                <tr>
                  <td style=""width:5px;background:{navy};border-radius:4px 0 0 4px;"">&nbsp;</td>
                  <td style=""background:#f4f6f9;padding:16px 18px;border-radius:0 8px 8px 0;border:1px solid rgba(0,33,71,0.08);border-left:none;"">
                    <p style=""margin:0;font-size:14px;color:{muted};line-height:1.55;"">Open your <strong style=""color:{navy};"">student portal</strong> to view the upload.</p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style=""background:{navy};padding:20px 24px;text-align:center;"">
              <div style=""font-size:13px;font-weight:700;color:#ffffff;"">Thank you.</div>
              <div style=""margin-top:10px;font-size:11px;color:#8fa8c4;line-height:1.4;"">{safeSchool} &mdash; Learning Management System</div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        public static string BuildNewAnnouncementHtml(
            string studentDisplayName,
            string courseName,
            string classCode,
            string announcementText,
            string schoolName,
            string? logoAbsoluteUrl)
        {
            static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            const string navy = "#002147";
            const string subBanner = "#F0F4F8";
            const string subBannerBorder = "#cfd8e3";
            const string bodyText = "#0b213a";
            const string muted = "#5f6f82";

            var safeName = E(studentDisplayName);
            var safeCourse = E(courseName);
            var safeCode = E(classCode);
            var safeSchool = E(string.IsNullOrWhiteSpace(schoolName) ? "Sta. Lucia Senior High School" : schoolName);
            var safeText = E(string.IsNullOrWhiteSpace(announcementText) ? "(No message)" : announcementText);

            var logoBlock = !string.IsNullOrWhiteSpace(logoAbsoluteUrl)
                ? $@"<img src=""{E(logoAbsoluteUrl)}"" alt=""{safeSchool} logo"" width=""56"" height=""56"" style=""display:block;margin:0 auto 10px;border-radius:50%;background:#ffffff;padding:6px;object-fit:contain;box-sizing:border-box;"" />"
                : $@"<div style=""width:56px;height:56px;margin:0 auto 10px;border-radius:50%;background:rgba(255,255,255,0.12);border:1px solid rgba(255,255,255,0.25);display:flex;align-items:center;justify-content:center;font-size:10px;color:rgba(255,255,255,0.85);text-align:center;line-height:1.2;padding:6px;box-sizing:border-box;"">Logo</div>
                    <div style=""font-size:11px;color:rgba(255,255,255,0.75);margin-bottom:8px;"">Sta. Lucia SHS</div>";

            return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/><title>New announcement</title></head>
<body style=""margin:0;padding:24px 12px;background:#e8ecf1;font-family:Segoe UI,Helvetica Neue,Arial,sans-serif;-webkit-font-smoothing:antialiased;"">
  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""max-width:600px;width:100%;border-collapse:collapse;border-radius:16px;overflow:hidden;box-shadow:0 12px 40px rgba(0,33,71,0.12);"">
          <tr>
            <td style=""background:{navy};padding:28px 24px 26px;text-align:center;"">
              {logoBlock}
              <div style=""font-size:22px;font-weight:800;color:#ffffff;letter-spacing:-0.02em;line-height:1.2;"">{safeSchool}</div>
              <div style=""margin-top:8px;font-size:11px;font-weight:600;color:#a8bdd9;letter-spacing:0.14em;text-transform:uppercase;"">Learning Management System</div>
            </td>
          </tr>
          <tr>
            <td style=""background:{subBanner};padding:22px 28px 18px;border-bottom:1px solid {subBannerBorder};"">
              <div style=""font-size:20px;font-weight:800;color:{navy};letter-spacing:-0.02em;line-height:1.2;"">New Announcement</div>
              <div style=""margin-top:8px;font-size:14px;color:{bodyText};line-height:1.45;"">There’s a new message from your instructor.</div>
            </td>
          </tr>
          <tr>
            <td style=""background:#ffffff;padding:28px 28px 24px;color:{bodyText};"">
              <p style=""margin:0 0 14px;font-size:15px;line-height:1.5;"">Greetings, <strong style=""color:{navy};"">{safeName}</strong>,</p>
              <p style=""margin:0 0 14px;font-size:15px;line-height:1.5;"">A new announcement was posted in <strong style=""color:{navy};"">{safeCourse}</strong> <span style=""color:{muted};font-weight:600;"">({safeCode})</span>.</p>
              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""border-collapse:collapse;margin-top:14px;"">
                <tr>
                  <td style=""background:#f4f6f9;border:1px solid rgba(0,33,71,0.08);border-radius:12px;padding:18px 18px;"">
                    <div style=""font-size:12px;color:{muted};font-weight:700;letter-spacing:0.12em;text-transform:uppercase;"">Message</div>
                    <div style=""margin-top:10px;font-size:15px;line-height:1.55;color:{bodyText};white-space:pre-wrap;"">{safeText}</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          <tr>
            <td style=""background:{navy};padding:20px 24px;text-align:center;"">
              <div style=""font-size:13px;font-weight:700;color:#ffffff;"">Thank you.</div>
              <div style=""margin-top:10px;font-size:11px;color:#8fa8c4;line-height:1.4;"">{safeSchool} &mdash; Learning Management System</div>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }
    }
}
