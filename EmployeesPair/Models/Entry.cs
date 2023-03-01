#nullable disable warnings

namespace EmployeesPair.Models
{
    public class Entry
    {
        public int EmpID { get; set; }

        public int ProjectID { get; set; }

        public DateOnly DateFrom { get; set; }

        public DateOnly? DateTo { get; set; }
    }
}