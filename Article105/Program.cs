using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MyLibrary.Collections;
using MyMath;
using Double = MyLibrary.Types.Double;
using Int32 = MyLibrary.Types.Int32;

namespace Article105
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Параметры программы
            string inputFileName = "input.csv";
            string outputFileName = "output.csv";
            double unit = 1.0;
            double epsilon = 0.0000000001;

            var nodeList = new StackListQueue<string>();
            var matrix = new Matrix<double>();

            // Определение параметров программы
            for (int i = 0; i < args.Length; i++)
            {
                if (string.CompareOrdinal(args[i], "-s") == 0
                    || string.CompareOrdinal(args[i], "-i") == 0)
                {
                    inputFileName = args[++i];
                }
                else if (string.CompareOrdinal(args[i], "-d") == 0
                         || string.CompareOrdinal(args[i], "-o") == 0)
                {
                    outputFileName = args[++i];
                }
                else if (string.CompareOrdinal(args[i], "-b1") == 0) unit = 1.0;
                else if (string.CompareOrdinal(args[i], "-b100") == 0) unit = 100.0;
                else if (string.CompareOrdinal(args[i], "-e") == 0)
                {
                    epsilon = Math.Pow(0.1, Int32.ParseAsString(args[++i]));
                }
            }

            // Загрузка матрицы из файла
            int lines = 0;
            using (StreamReader reader = File.OpenText(inputFileName))
            {
                // регулярное выражение для разбора полей исходного файла
                var regex = new Regex(@"\s*(?<from>[^;]+)\s*;\s*(?<to>[^;]+)\s*;\s*(?<value>\d+([,.]\d*)?)\s*");
                for (string line = reader.ReadLine();; line = reader.ReadLine())
                {
                    lines++;
                    Match match = regex.Match(line);
                    int i = nodeList.IndexOf(match.Groups["from"].Value);
                    if (i == -1)
                    {
                        i = nodeList.Count;
                        if (!nodeList.Any())
                        {
                            matrix.Add(new Vector<double> {0.0});
                        }
                        else
                        {
                            matrix.AddColumn();
                            matrix.AddRow();
                        }
                        nodeList.Add(match.Groups["from"].Value);
                        Debug.WriteLine("Зарегистрирован участник {0} под номером {1}", nodeList.Last(), i);
                        Debug.WriteLine("Текущий размер матрицы {0}x{1}", matrix.Rows, matrix.Columns);
                    }
                    int j = nodeList.IndexOf(match.Groups["to"].Value);
                    if (j == -1)
                    {
                        j = nodeList.Count;
                        matrix.AddColumn();
                        matrix.AddRow();
                        nodeList.Add(match.Groups["to"].Value);
                        Debug.WriteLine("Зарегистрирован участник {0} под номером {1}", nodeList.Last(), j);
                        Debug.WriteLine("Текущий размер матрицы {0}x{1}", matrix.Rows, matrix.Columns);
                    }
                    double value = Double.ParseAsString(match.Groups["value"].Value);
                    matrix[i][j] = value/unit;
                    if (reader.EndOfStream) break;
                }
                reader.Close();
            }

            Console.WriteLine("Прочитано строк = {0}", lines);

            // Проверка исходных данных
            Debug.Assert(matrix.Rows == matrix.Columns);
            Debug.Assert(matrix.All(row => row.All(x => x >= 0.0 - epsilon && x <= 1.0 + epsilon)));

            int total = Math.Max(matrix.Rows, matrix.Columns);

#if DEBUG
            // Проверка исходных данных
            for (int i = 0; i < total; i++)
            {
                double s = 0.0;
                for (int j = 0; j < total; j++) s += matrix[j][i];
                Debug.Assert(s <= 1.0 + epsilon);
            }
#endif

            // Определение компонент связанности
            var groupList = new StackListQueue<StackListQueue<int>>();

            // Инициализация определения компонент связанности
            for (int i = 0; i < total; i++) groupList.Add(new StackListQueue<int> {i});
            for (int i = groupList.Count - 1; i > 0; i--)
            {
                for (int j = groupList.Count - 1; j >= i; j--)
                {
                    // Проверяем существование путей из одной группы в другую группу
                    if (Matrix<double>.IsZero(matrix.SubMatrix(groupList[j - i], groupList[j]))
                        && Matrix<double>.IsZero(matrix.SubMatrix(groupList[j], groupList[j - i]))) continue;

                    // Если существует путь между двумя группами
                    // то объединяем группы в одну
                    groupList[j - i].AddRange(groupList[j]);
                    groupList.RemoveAt(j);
                    Debug.WriteLine("Группа {0} присоеденена к группе {1}", j, j - i);
                    i = groupList.Count;
                    break;
                }
            }
            Console.WriteLine("Обнаружено {0} групп", groupList.Count);
#if DEBUG
            for (int i = 0; i < groupList.Count; i++)
            {
                Debug.WriteLine("Группа #{0}", i);
                foreach (int j in groupList[i]) Debug.WriteLine(nodeList[j]);
            }
#endif

            using (StreamWriter writer = File.CreateText(outputFileName))
            {
                // Для каждой компоненты связанности
                foreach (var group in groupList)
                {
                    int n = group.Count;

                    // Отсекаются группы из одного участника
                    if (n == 1) continue;

                    Console.WriteLine("Анализируется группа из {0} участников", n);

                    var e = new Matrix<double>(n, n);
                    for (int i = 0; i < n; i++) e[i][i] = 1.0;

                    // Получение выборки из исходной матрицы E-A
                    Matrix<double> a = matrix.SubMatrix(group, group);
                    Matrix<double> b = e - a;

#if DEBUG
                    // Сохраняем матрицу для дальнейшей самопроверки
                    var z = new Matrix<double>(b.Select(row => new Vector<double>(row)));
                    Debug.Assert(z.Rows == n);
                    Debug.Assert(z.Columns == n);
#endif

                    // Дописывание к выборке единичной матрицы справа
                    b.AppendColumns(e);

                    Debug.Assert(b.Rows == n);
                    Debug.Assert(b.Columns == 2*n);


                    // Приведение выборки к каноническому виду преобразованиями по строкам
                    b.GaussJordan(
                        Matrix<double>.Search.SearchByRows,
                        Matrix<double>.Transform.TransformByRows,
                        0, n);

                    Debug.Assert(b.All(row => !Vector<double>.IsZero(new Vector<double>(row.GetRange(0, n)))));

                    // Сортировка строк для приведения канонической матрицы к единичной матрице
                    var dic = new Dictionary<int, Vector<double>>();
                    for (int i = 0; i < n; i++)
                    {
                        int j = 0;
                        Vector<double> vector = b[i];
                        while (Vector<double>.IsZero(vector[j])) j++;
                        dic.Add(j, vector);
                    }

                    // Получение обратной матрицы для E-A
                    var c = new Matrix<double>();
                    for (int i = 0; i < n; i++) c.Add(new Vector<double>(dic[i].GetRange(n, n)));

#if DEBUG
                    // Проверяем, что полученная матрица действительно является обратной
                    Matrix<double> y = c*z;
                    Debug.Assert(y.Rows == n);
                    Debug.Assert(y.Columns == n);
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                            if (i == j)
                            {
                                Debug.Assert(Math.Abs(y[i][j] - 1.0) < epsilon);
                            }
                            else
                            {
                                Debug.Assert(Math.Abs(y[i][j] - 0.0) < epsilon);
                            }
                    Console.WriteLine("Сверка вычислений произведена");
#endif

                    Debug.Assert(c.Rows == c.Columns);

                    // Проверка выходных данных
#if DEBUG
                    //for (int i = 0; i < n; i++)
                    //{
                    //    for (int j = 0; j < n; j++)
                    //    {
                    //        if (i == j) continue;
                    //        Debug.Assert(c[i][j] >= 0.0 - epsilon && c[i][j] <= 1.0 + epsilon);
                    //    }
                    //}
#endif

                    // Выгрузка ненулевых элементов обратной матрицы в файл
                    for (int i = 0; i < n; i++)
                    {
                        for (int j = 0; j < n; j++)
                        {
                            if (i == j) continue;
                            if (Matrix<double>.IsZero(c[i][j])) continue;

                            // Сохранение полей в том же формате, что и исходные данные
                            writer.Write(nodeList[group[i]]);
                            writer.Write(";");
                            writer.Write(nodeList[group[j]]);
                            writer.Write(";");
                            writer.Write(c[i][j].ToString().Replace(",", "."));
                            writer.WriteLine();
                        }
                    }
                }
            }
        }
    }
}