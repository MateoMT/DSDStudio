using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace IDE.Services
{
    public interface IFileService
    {
        string OpenFileDialog(string filter);
        string SaveFileDialog(string filter);
    }
    public class FileService : IFileService
    {
        public string OpenFileDialog(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = false
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
        public string SaveFileDialog(string filter)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                AddExtension = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
