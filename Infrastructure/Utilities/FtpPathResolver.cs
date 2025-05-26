using Core.Entities.RecycleLotCopy;
using Core.Interfaces;

namespace Infrastructure.Utilities
{
    public static class FtpPathResolver
    {
        public static string ResolvePath(FtpPathSetting setting, string lotNo, string productNo = null)
        {
            if (string.IsNullOrWhiteSpace(setting.FilePath))
                return string.Empty;

            var result = setting.FilePath.Replace("{LOTNO}", lotNo);
            if (!string.IsNullOrWhiteSpace(productNo))
                result = result.Replace("{PN}", productNo);

            return result;
        }
    }
}
