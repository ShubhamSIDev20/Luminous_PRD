using BatteryTestingSystem.Components.UI;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class ProgramDTO
    {
        public long ProgramId { get; set; }
        public string ProgramName { get; set; }
        public string? Description { get; set; }
        public float? MaxAh { get; set; }
        public long? ProgramTimeTicks { get; set; }
        public int? ProgramSteps { get; set; }
        public string ProgramJson { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
        public string? ProgramHash { get; set; }

        public bool IsVaild { get; set; } = false;
        public List<StepModel> ProgramStepModel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ProgramJson))
                    return new List<StepModel>();

                try
                {
                    return JsonConvert.DeserializeObject<List<StepModel>>(ProgramJson) ?? new List<StepModel>();
                }
                catch
                {
                    // Optional: log the error
                    return new List<StepModel>();
                }
            }
            set
            {
                try
                {
                    ProgramJson = JsonConvert.SerializeObject(value ?? new List<StepModel>());
                }
                catch
                {
                    // Optional: log the error
                    ProgramJson = "[]"; // fallback to empty array
                }
            }
        }

    }
}
