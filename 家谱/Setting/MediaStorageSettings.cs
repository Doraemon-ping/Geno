namespace 家谱.Setting
{
    public class MediaStorageSettings
    {
        public string RootPath { get; set; } = "data/uploads";

        public string RequestPath { get; set; } = "/file";

        public int MaxFileSizeMb { get; set; } = 200;

        public string ResolveRootPath(string contentRootPath)
        {
            if (Path.IsPathRooted(RootPath))
            {
                return RootPath;
            }

            return Path.GetFullPath(Path.Combine(contentRootPath, RootPath));
        }
    }
}
