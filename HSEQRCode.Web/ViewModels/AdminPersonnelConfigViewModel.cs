using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HSEQRCode.Web.ViewModels
{
    public class AdminPersonnelConfigViewModel
    {
        public AdminPersonnelConfigViewModel()
        {
            AssignedPersonnel = new List<FormPersonnelItemViewModel>();
            AvailableUsers = new List<AppUserOptionViewModel>();
        }

        public int DataSourceId { get; set; }

        public string DataSourceName { get; set; }

        public List<FormPersonnelItemViewModel> AssignedPersonnel { get; set; }

        public List<AppUserOptionViewModel> AvailableUsers { get; set; }

        public string SelectedUsername { get; set; }

        [MaxLength(100)]
        public string NewUsername { get; set; }

        [MaxLength(100)]
        public string NewPassword { get; set; }

        [MaxLength(200)]
        public string NewFullName { get; set; }

        public bool NewUserIsAdmin { get; set; }
    }

    public class FormPersonnelItemViewModel
    {
        public int Id { get; set; }

        public string Username { get; set; }

        public string FullName { get; set; }

        public bool IsActive { get; set; }

        public string AssignedAtJalali { get; set; }
    }

    public class AppUserOptionViewModel
    {
        public string Username { get; set; }

        public string FullName { get; set; }

        public bool IsAdmin { get; set; }
    }
}
