using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class TableOperatorFileDto
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string? Description { get; set; }

        public string FilePath { get; set; }

        public long Size { get; set; }

        public string Data { get; set; }

    }
}
