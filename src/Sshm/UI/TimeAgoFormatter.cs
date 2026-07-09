namespace Sshm.UI;

internal static class TimeAgoFormatter
{
    internal static string Format(DateTime time)
    {
        TimeSpan duration = DateTime.Now - time;

        if (duration < TimeSpan.FromMinutes(1))
        {
            int seconds = (int)duration.TotalSeconds;
            return seconds <= 1 ? "1 second ago" : $"{seconds} seconds ago";
        }

        if (duration < TimeSpan.FromHours(1))
        {
            int minutes = (int)duration.TotalMinutes;
            return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
        }

        if (duration < TimeSpan.FromDays(1))
        {
            int hours = (int)duration.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (duration < TimeSpan.FromDays(7))
        {
            int days = (int)duration.TotalHours / 24;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        if (duration < TimeSpan.FromDays(30))
        {
            int weeks = (int)duration.TotalHours / (24 * 7);
            return weeks == 1 ? "1 week ago" : $"{weeks} weeks ago";
        }

        if (duration < TimeSpan.FromDays(365))
        {
            int months = (int)duration.TotalHours / (24 * 30);
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }

        int years = (int)duration.TotalHours / (24 * 365);
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }
}
