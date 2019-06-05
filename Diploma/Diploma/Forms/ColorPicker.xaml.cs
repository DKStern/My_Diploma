using Diploma.Classes;
using System;
using System.Drawing;
using System.Windows;

namespace Diploma.Forms
{
    public partial class ColorPicker : Window
    {
        public ColorData colorData;
        private Logger logger;

        public ColorPicker(ColorData data, string log)
        {
            InitializeComponent();
            colorData = data;
            logger = new Logger($"{log}\\ColorPicker");
            logger.Add($"Открыто окно задания диапазонов цветов");
            logger.Add($"Время начала: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Add("");

            foreach (var color in colorData.ColorList)
            {
                colorView.Items.Add(color);
            }
        }

        private void addColorBtn_Click(object sender, RoutedEventArgs e)
        {
            if (startBox.Text != "" && finishBox.Text != "")
            {
                int a, b;
                if (Int32.TryParse(startBox.Text, out a) && Int32.TryParse(finishBox.Text, out b))
                {
                    if (a >= 0 && b <= 100 && a <=b)
                    {
                        if (colorData.AddColor(a, b, Color.FromArgb(255, startColorPicker.SelectedColor.Value.R, startColorPicker.SelectedColor.Value.G, startColorPicker.SelectedColor.Value.B), Color.FromArgb(255, finishColorPicker.SelectedColor.Value.R, finishColorPicker.SelectedColor.Value.G, finishColorPicker.SelectedColor.Value.B)))
                        {
                            logger.Add($"ColorPicker >> Новый диапазон успешно добавлен:\n\tДиапазон: от {a}% до {b}%\n\tНачальный цвет: ({startColorPicker.SelectedColor.Value.R},{startColorPicker.SelectedColor.Value.G},{startColorPicker.SelectedColor.Value.B})\n\tКонечный цвет: ({ finishColorPicker.SelectedColor.Value.R},{ finishColorPicker.SelectedColor.Value.G},{ finishColorPicker.SelectedColor.Value.B})");
                            colorView.Items.Add(colorData.ColorList[colorData.ColorList.Count - 1]);
                        }
                    }
                    else
                    {
                        logger.Add($"ColorPicker >> Границы диапазона были заданы неверно!");
                    }
                }
                else
                {
                    MessageBox.Show("Недопустимые символы в значениях диапазона!\nВведите значения от 0 до 100");
                    logger.Add($"ColorPicker >> Границы диапазона содержат недопустимые символы!");
                }
            }
        }

        private void deleteColorBtn_Click(object sender, RoutedEventArgs e)
        {
            var index = colorView.SelectedIndex;
            if (index != -1)
            {
                logger.Add($"ColorPicker >> Удалён диапозон цвета от {colorData.ColorList[index].Start}% до {colorData.ColorList[index].Finish}%");
                colorData.ColorList.RemoveAt(index);
                colorView.Items.RemoveAt(index);
            }
        }

        private void oKBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            logger.Add($"Завершение: {DateTime.Now.ToUniversalTime()} UTC");
            logger.Flush();
            logger.Close();
        }
    }
}
