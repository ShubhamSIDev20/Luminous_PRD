namespace BatteryTestingSystem.Utils
{
    public static class CN
    {
        public static string Merge(params string?[] classes)
        {
            var filteredClasses = classes
           .Where(c => !string.IsNullOrWhiteSpace(c))
           .SelectMany(c => c!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
           .ToList();

            // Simple merge - more sophisticated Tailwind merging can be added if needed
            return string.Join(" ", filteredClasses.Distinct()); 
        }
    }
}
