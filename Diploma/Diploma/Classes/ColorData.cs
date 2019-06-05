using System.Collections.Generic;
using System.Drawing;
using System.Windows;

namespace Diploma.Classes
{
    public class ColorData
    {
        public List<SelectedColor> ColorList { get; } = new List<SelectedColor>();

        /// <summary>
        /// Проверяет, будет ли пересекаться данный диапазон с имеющимися
        /// </summary>
        /// <param name="start">Начальное значение диапазона</param>
        /// <param name="finish">Конечное значение диапазона</param>
        /// <returns>Пересекается ли с имеющимися диапазонами или нет</returns>
        public bool IsCrossing(int start, int finish)
        {
            foreach (var color in ColorList)
            {
                if (start >= color.Start && finish <= color.Finish)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Добавление нового цвета
        /// </summary>
        /// <param name="start">Начало диапазона (в процентах)</param>
        /// <param name="finish">Конец диапазона (в процентах)</param>
        /// <param name="startColor">Начальный цвет</param>
        /// <param name="finishColor">Конечный цвет</param>
        /// <returns>Возращает добавлен ли цвет или нет</returns>
        public bool AddColor(int start, int finish, Color startColor, Color finishColor)
        {
            if (start >= finish)
            {
                MessageBox.Show("Значение начала диапазона больше или равено значению конца диапазона!\nЦвет не добавлен!");
                return false;
            }
            if (IsCrossing(start, finish))
            {
                MessageBox.Show("Введённый диапазон накладывается на уже существующий!\nЦвет не добавлен!");
                return false;
            }
            if (startColor != null && finishColor != null)
            {
                ColorList.Add(new SelectedColor(start, finish, startColor, finishColor));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Удаляет цвет из списка
        /// </summary>
        /// <param name="num">Индекс цвета в списке</param>
        public void DeleteColor(int num)
        {
            ColorList.RemoveAt(num);
        }
    }
}
