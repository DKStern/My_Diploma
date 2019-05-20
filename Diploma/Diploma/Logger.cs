using System;
using System.IO;

namespace Diploma
{
    public class Logger
    {
        private string Path; //Путь сохраняемого лога
        private StreamWriter sw; 

        /// <summary>
        /// Конструктор логгера
        /// </summary>
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
        async public void Add(string str)
        {
            await sw.WriteLineAsync(str);
        }

        /// <summary>
        /// Закрывает логгер
        /// </summary>
        public void Close()
        {
            sw.Close();
        }

    }
}
