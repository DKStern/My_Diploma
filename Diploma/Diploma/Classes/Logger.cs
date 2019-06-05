using System.IO;

namespace Diploma.Classes
{
    public class Logger
    {
        private string Path; //Путь сохраняемого лога
        private StreamWriter sw; 

        /// <summary>
        /// Конструктор логгера
        /// </summary>
        public Logger(string name)
        {
            Path = $"{name}.txt";
            //if(File.Exists(Path))
            //{
            //    File.Delete(Path);
            //    File.Create(Path).Close();
            //}
            //else
            //{
            //    File.Create(Path).Close();
            //}

            sw = new StreamWriter(Path, true);
        }

        /// <summary>
        /// Дозаписывает оставшееся из буферов
        /// </summary>
        public void Flush()
        {
            sw.Flush();
        }

        /// <summary>
        /// Добавить запись в логгер
        /// </summary>
        /// <param name="str">Запись</param>
        public void Add(string str)
        {
            sw.WriteLineAsync(str);
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
