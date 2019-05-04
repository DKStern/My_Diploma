using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace Diploma
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Drawing.Color StartColor, FinishColor;
        private int fileCount = 0; //Count of files
        private string path; //Path to the directory with data
        private WriteableBitmap bitmap;
        private int depth = 1291, frequency = 10000;
        Logger logger = new Logger();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            imageBox.Source = bitmap;
            logger.Add($"Начало работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add("");
        }

        private void exitBtn_Click(object sender, RoutedEventArgs e)
        {
            logger.Add($"Завершение работы программы: {DateTime.Now.ToUniversalTime()} UTC");
            Close();
        }

        /// <summary>
        /// Создаёт изображение из файл с данными
        /// </summary>
        private void CreateImg()
        {
            logger.Add("Визуализация данных...");
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int width, heigth;
            double max = double.MinValue, min = double.MaxValue;
            System.Drawing.Color color;
            List<string> file = new List<string>();
            string[] str;
            int count = 0;
            int len = 0;
            using (StreamReader sr = new StreamReader("OutPut.txt"))
            {
                logger.Add("Нахождение диапозона значения энергии...");
                var st = new Stopwatch();
                st.Start();
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    count++;
                    if (line[line.Length - 1] == ' ')
                        line = line.Remove(line.Length - 1, 1);
                    str = line.Split(' ');
                    if (len < str.Length)
                        len = str.Length;
                    for (int i = 0; i < str.Length; i++)
                    {
                        var d = Convert.ToDouble(str[i]);
                        if (d > max)
                        {
                            max = d;
                        }
                        if (d < min)
                        {
                            min = d;
                        }
                    }
                }
                st.Stop();
                logger.Add($"Диапазон найден: от {min} до {max}");
                logger.Add($"Нахождение заняло: {st.Elapsed}");
            }
            heigth = count;
            width = len;
            Bitmap bmp = new Bitmap(width, heigth);
            int y = 0;
            using (var sr = new StreamReader("Output.txt"))
            {
                logger.Add("Создание изображения из файла...");
                var st = new Stopwatch();
                st.Start();
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line[line.Length - 1] == ' ')
                        line = line.Remove(line.Length - 1, 1);
                    str = line.Split(' ');
                    for (int x = 0; x < str.Length; x++)
                    {
                        color = GetColors(min, max, Convert.ToDouble(str[x]));
                        bmp.SetPixel(x, y, color);
                    }
                    y++;
                }
                st.Stop();
                logger.Add("Изображение создано");
                logger.Add($"Создание изображения заняло: {st.Elapsed}");
            }

            bmp.Save("Pic.png");
            bmp.Dispose();
            watch.Stop();
            logger.Add("Изображение сохранено: Pic.png");
            ShowImg();
            logger.Add($"Визуализация заняла: {watch.Elapsed}");
            logger.Add("");
            GC.Collect(1, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Вычисляем FFT
        /// </summary>
        private void FFT()
        {

        }

        /// <summary>
        /// Приведение размерности входных данных до ближайшей степени двойки
        /// </summary>
        /// <param name="soundLine">Входные данные</param>
        /// <param name="K">Степень</param>
        /// <returns>Новые данные</returns>
        private short[] PowOfTwo(short[] soundLine, ref short K)
        {
            logger.Add("Приводим размерность входных данных к степени двойки...");
            int len = soundLine.Length;
            uint pow = 2;
            short k = 1;
            while (len > pow)
            {
                pow *= 2;
                k++;
            }

            K = k;
            if (len == pow)
                return soundLine;
            else
            {
                short[] newMas = new short[pow];
                for (int i = 0; i < pow; i++)
                {
                    if (i < len)
                        newMas[i] = soundLine[i];
                    else
                        newMas[i] = 0;
                }

                logger.Add("Размерность данных приведена к степени двойки! Показатель степени: " + K.ToString());
                return newMas;
            }
        }

        /// <summary>
        /// Вычисления
        /// </summary>
        private void Start()
        {
            logger.Add("Получаем список файлов...");
            string[] fileList;
            fileList = Directory.GetFiles(path);

            byte[] byteMas;
            short[] soundLine;
            byte[] b = new byte[2];
            short K = 0; //Показатель степени

            for (int i = 1; i <= depth; i++)
            {
                logger.Add("Читаем данные из " + i.ToString() + " файла...");
                byteMas = File.ReadAllBytes(fileList[i - 1]);
                soundLine = new short[byteMas.Length / 2];
                for (int j = 0; j < byteMas.Length; j += 2)
                {
                    b[0] = byteMas[j];
                    b[1] = byteMas[j + 1];
                    soundLine[j / 2] = BitConverter.ToInt16(b, 0);
                }
                logger.Add("Данные считаны!");
                soundLine = (short[])PowOfTwo(soundLine, ref K).Clone();

                //GPU
            }
        }

        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            //Вычисления на GPU
            Start();

            CreateImg();  
        }

        /// <summary>
        /// Отображение изображения
        /// </summary>
        private void ShowImg()
        {
            BitmapImage bm1 = new BitmapImage();
            bm1.BeginInit();
            bm1.UriSource = new Uri("Pic.png", UriKind.Relative);
            bm1.CacheOption = BitmapCacheOption.OnLoad;
            bm1.EndInit();
            bitmap = new WriteableBitmap(bm1);
            imageBox.Source = bitmap;
        }

        private static System.Drawing.Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return System.Drawing.Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return System.Drawing.Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return System.Drawing.Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return System.Drawing.Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return System.Drawing.Color.FromArgb(255, t, p, v);
            else
                return System.Drawing.Color.FromArgb(255, v, p, q);
        }

        private void startColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            StartColor = System.Drawing.Color.FromArgb(255, startColorPiker.SelectedColor.Value.R, startColorPiker.SelectedColor.Value.G, startColorPiker.SelectedColor.Value.B);
            logger.Add($"Начальный цвет выбран: {StartColor.ToString()}");
            logger.Add("");
        }

        private void finishColorPiker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            FinishColor = System.Drawing.Color.FromArgb(255, finishColorPiker.SelectedColor.Value.R, finishColorPiker.SelectedColor.Value.G, finishColorPiker.SelectedColor.Value.B);
            logger.Add($"Конечный цвет выбран: {FinishColor.ToString()}");
            logger.Add("");
        }

        private void openBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                logger.Add("Ожидается выбор папки...");
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    path = folderBrowserDialog.SelectedPath;
                    fileCount = Directory.GetFiles(path).Length;
                    depthFinishBox.Text = Convert.ToString(fileCount);
                    progressBar.Value = 0;
                    progressBar.Maximum = fileCount;
                    //helpMas = null;
                    logger.Add($"Папка выбрана: {path}");
                    logger.Add($"Файлов в папке: {fileCount.ToString()}");
                    logger.Add("");
                }
                else
                {
                    logger.Add("Папка не выбрана!");
                    logger.Add("");
                }
            }
            catch
            {
                logger.Add("Ошибка при открытии папки!");
                logger.Add("");
                System.Windows.MessageBox.Show("Ошибка при открытии папки!");
            }
        }

        private void imageBox_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pointStart = imageBox.TranslatePoint(new System.Windows.Point(0,0),this);
            var pointFinish = new System.Windows.Point(imageBox.ActualWidth, imageBox.ActualHeight);
            var mousePoint = new System.Windows.Point(e.GetPosition(null).X, e.GetPosition(null).Y);
            var xNew = (mousePoint.X - pointStart.X) / imageBox.ActualWidth;
            var yNew = (mousePoint.Y - pointStart.Y) / imageBox.ActualHeight;
            XLabel.Content = $"Частота: {Convert.ToInt32(xNew * frequency)} Гц";
            YLabel.Content = $"Глубина: {Convert.ToInt32(yNew * depth)} метров";
        }

        /// <summary>
        /// Вычисление цвета пикселя
        /// </summary>
        /// <param name="min">Минимальное значение</param>
        /// <param name="max">Максимальное значение</param>
        /// <param name="current">Текущее значение</param>
        /// <returns>Цвет</returns>
        private System.Drawing.Color GetColors(double min, double max, double current)
        {
            System.Drawing.Color color;
            var help1 = max - min;
            var help2 = current - min;
            var help = help2 / help1;

            if (current == -1.0)
                return StartColor;

            //double hStart = StartColor.GetHue();
            //double hFinish = FinishColor.GetHue();
            //var h = hFinish - hStart;
            //h = h * help + min;
            //color = ColorFromHSV(h, 1, 1);

            var dR = Convert.ToInt32(Math.Ceiling(help * (FinishColor.R - StartColor.R)));
            var dG = Convert.ToInt32(Math.Ceiling(help * (FinishColor.G - StartColor.G)));
            var dB = Convert.ToInt32(Math.Ceiling(help * (FinishColor.B - StartColor.B)));
            color = System.Drawing.Color.FromArgb(dR + StartColor.R, dG + StartColor.G, dB + StartColor.B);

            return color;
        }
    }
}
