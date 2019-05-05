using System;
using System.IO;

namespace Diploma
{
    public class Logger
    {
        private string Path;
        private StreamWriter sw;

        public Logger()
        {
            Path = $"Log {DateTime.Now.Day}_{DateTime.Now.Month}_{DateTime.Now.Year} {DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}.txt";
            if(File.Exists(Path))
            {
                File.Delete(Path);
                File.Create(Path).Close();
            }
            else
            {
                File.Create(Path).Close();
            }

            sw = new StreamWriter(Path, true);
        }

        /// <summary>
        /// Добавить запись в логгер
        /// </summary>
        /// <param name="str">Запись</param>
        public void Add(string str)
        {
            //using (var sw = new StreamWriter(Path, true))
            //{
            //    sw.WriteLine(str);
            //}

            sw.WriteLine(str);
        }

    }
}
