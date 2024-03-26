
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Diagnostics;

namespace QAMModulationConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Por favor, ingrese la ruta del archivo: ");
            string filePath = Console.ReadLine();

            if (!File.Exists(filePath))
            {
                Console.WriteLine("El archivo especificado no existe.");
                return;
            }

            Console.Write("Por favor, ingrese 'modular' para modulación o 'demodular' para demodulación: ");
            string mode = Console.ReadLine()?.ToLower();

            Console.Write("Por favor, ingrese el número de bits por símbolo para la modulación/demodulación: ");
            if (!int.TryParse(Console.ReadLine(), out int bitsPerSymbol) || bitsPerSymbol <= 0)
            {
                Console.WriteLine("Número de bits por símbolo no válido.");
                return;
            }

            Console.Write("Por favor, ingrese el valor de SNR (en dB) para simular el ruido, o 'none' para omitir: ");
            string snrInput = Console.ReadLine();
            bool addNoise = double.TryParse(snrInput, out double snrDb);

            byte[] fileBytes = File.ReadAllBytes(filePath);
            Console.WriteLine($"Se ha convertido el archivo en un flujo de {fileBytes.Length * 8} bits.");
            QamModulator modulator = new QamModulator();
            QamDemodulator demodulator = new QamDemodulator();
            Stopwatch stopwatch = new Stopwatch();

            try
            {
              
                List<Complex> modulatedSignal;
                byte[] demodulatedBytes;
                switch (mode)
                {
                    case "modular":
                        Console.WriteLine("Modulando...");
                        stopwatch.Start();
                        modulatedSignal = modulator.Modulate(fileBytes, bitsPerSymbol);
                        if (addNoise)
                        {
                            modulatedSignal = AddGaussianNoise(modulatedSignal, snrDb);
                            Console.WriteLine($"Se añadió ruido con un SNR de {snrDb} dB a la señal modulada.");
                        }
                        stopwatch.Stop();
                        Console.WriteLine($"La modulación tomó: {stopwatch.ElapsedMilliseconds} milisegundos.");

                        string csvModFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_modulatedSignalParameters.csv");
                        modulator.SaveSignalParametersToCsv(modulatedSignal, csvModFilePath, 500);
                        Console.WriteLine($"Se han guardado los parámetros de la señal en: {csvModFilePath}");

                        string modulatedFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_modulated.bin");
                        SaveModulatedSignal(modulatedSignal, modulatedFilePath);
                        Console.WriteLine($"Señal modulada guardada en: {modulatedFilePath}");
                        break;

                    case "demodular":
                        Console.WriteLine("Demodulando...");
                        stopwatch.Restart();
                        string demodulatedFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_modulated.bin");
                        modulatedSignal = LoadModulatedSignal(demodulatedFilePath);

                        demodulatedBytes = demodulator.Demodulate(modulatedSignal, bitsPerSymbol);
                        stopwatch.Stop();
                        Console.WriteLine($"La demodulación tomó: {stopwatch.ElapsedMilliseconds} milisegundos.");

                        string demodFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_demodulated" + Path.GetExtension(filePath));
                        File.WriteAllBytes(demodFilePath, demodulatedBytes);
                        Console.WriteLine($"Archivo demodulado guardado como: {demodFilePath}");

                        string reportFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_comparisonReport.txt");
                        DataComparator.CompareDataAndSaveReport(fileBytes, demodulatedBytes, reportFilePath);
                        Console.WriteLine($"Reporte de comparación guardado en: {reportFilePath}");
                        break;

                    default:
                        Console.WriteLine("Operación no válida. Por favor, elija 'modular' o 'demodular'.");
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Se produjo un error durante el proceso: {ex.Message}");
            }

          

        }

        // Implementación de otros métodos y clases...

        public static List<Complex> AddGaussianNoise(List<Complex> modulatedSignal, double snrDb)
        {
            var noisySignal = new List<Complex>();
            double snrLinear = Math.Pow(10, snrDb / 10); // Convierte SNR de dB a lineal
            double noisePower = 1 / snrLinear;
            double noiseAmplitude = Math.Sqrt(noisePower / 2);
            Random rand = new Random();

            foreach (var symbol in modulatedSignal)
            {
                double noiseI = noiseAmplitude * BoxMuller(rand);
                double noiseQ = noiseAmplitude * BoxMuller(rand);
                noisySignal.Add(new Complex(symbol.Real + noiseI, symbol.Imaginary + noiseQ));
            }

            return noisySignal;
        }

        private static double BoxMuller(Random rand)
        {
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }



        static int ConvertQamModeToBits(string qamMode)
        {
            return qamMode switch
            {
                "1024qam" => 10,
                "4096qam" => 12,
                "65536qam" => 16,
                "1048576qam" => 20,
                "64bits" => 64, // Nuevo caso para manejar 64 bits por símbolo
                _ => throw new NotSupportedException($"El modo {qamMode} no es soportado.")
            };
        }


        static BitArray ConvertBytesToBitArray(byte[] bytes)
        {
            return new BitArray(bytes);
        }
        static void SaveModulatedSignal(List<Complex> signal, string filePath)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                foreach (Complex c in signal)
                {
                    writer.Write(c.Real);
                    writer.Write(c.Imaginary);
                }
            }
        }
        private static List<Complex> LoadModulatedSignal(string filePath)
        {
            List<Complex> modulatedSignal = new List<Complex>();

            using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    double realPart = reader.ReadDouble();
                    double imaginaryPart = reader.ReadDouble();
                    modulatedSignal.Add(new Complex(realPart, imaginaryPart));
                }
            }

            return modulatedSignal;
        }

    }

    public class QamModulator
    {
        public List<Complex> Modulate(byte[] bitStream, int bitsPerSymbol)
        {
            List<Complex> modulatedSymbols = new List<Complex>();
            int qamOrder = (int)Math.Pow(2, bitsPerSymbol);

            for (int i = 0; i < bitStream.Length * 8; i += bitsPerSymbol)
            {
                int symbolValue = BitArrayToInt(bitStream, i, bitsPerSymbol);
                Complex symbol = MapIntToComplex(symbolValue, qamOrder);
                modulatedSymbols.Add(symbol);
            }

            return modulatedSymbols;
        }

        private int BitArrayToInt(byte[] bitStream, int startBit, int bitsPerSymbol)
        {
            int value = 0;
            for (int i = 0; i < bitsPerSymbol; i++)
            {
                if (startBit + i < bitStream.Length * 8)
                {
                    int byteIndex = (startBit + i) / 8;
                    int bitIndex = (startBit + i) % 8;
                    int bit = (bitStream[byteIndex] >> (7 - bitIndex)) & 1;
                    value = (value << 1) + bit;
                }
            }
            return value;
        }

        private Complex MapIntToComplex(int symbolValue, int qamOrder)
        {
            int sqrtQam = (int)Math.Sqrt(qamOrder);
            double re = symbolValue % sqrtQam - sqrtQam / 2 + 0.5;
            double im = symbolValue / sqrtQam - sqrtQam / 2 + 0.5;

            // Crear el punto original
            Complex originalPoint = new Complex(re, im);

            // Definir el ángulo de rotación (por ejemplo, 10 grados convertidos a radianes)
            double rotationAngleRadians = 10 * (Math.PI / 180);

            // Aplicar rotación
            Complex rotatedPoint = originalPoint * Complex.FromPolarCoordinates(1, rotationAngleRadians);

            return rotatedPoint;
        }

        public void SaveSignalParametersToCsv(List<Complex> modulatedSignal, string filePath, int numberOfSymbols)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Amplitud,Fase"); // Encabezados para las columnas CSV

                for (int i = 0; i < numberOfSymbols && i < modulatedSignal.Count; i++)
                {
                    double amplitude = modulatedSignal[i].Magnitude; // Amplitud del símbolo complejo
                    double phase = Math.Atan2(modulatedSignal[i].Imaginary, modulatedSignal[i].Real); // Fase del símbolo complejo

                    // Escribir la amplitud y fase en formato CSV
                    writer.WriteLine($"{amplitude.ToString(CultureInfo.InvariantCulture)},{phase.ToString(CultureInfo.InvariantCulture)}");
                }
            }
        }

    }
    public class QamDemodulator
    {
        public byte[] Demodulate(List<Complex> modulatedSymbols, int bitsPerSymbol)
        {
            var bitStream = new List<byte>();
            int qamOrder = (int)Math.Pow(2, bitsPerSymbol);

            foreach (var symbol in modulatedSymbols)
            {
                int symbolValue = MapComplexToInt(symbol, qamOrder);
                var symbolBits = ConvertIntToBits(symbolValue, bitsPerSymbol);
                bitStream.AddRange(symbolBits);
            }

            return BitsToBytes(bitStream);
        }

        private int MapComplexToInt(Complex rotatedSymbol, int qamOrder)
        {
            // Definir el ángulo de rotación inverso (negativo para rotación opuesta)
            double rotationAngleRadians = -10 * (Math.PI / 180); // Mismo ángulo usado en la modulación, pero negativo

            // Aplicar rotación inversa
            Complex originalPoint = rotatedSymbol * Complex.FromPolarCoordinates(1, rotationAngleRadians);

            // Proceder con el mapeo del punto rotado de vuelta a un valor entero, como antes
            int sqrtQam = (int)Math.Sqrt(qamOrder);
            int re = (int)Math.Round(originalPoint.Real + sqrtQam / 2 - 0.5);
            int im = (int)Math.Round(originalPoint.Imaginary + sqrtQam / 2 - 0.5);

            return im * sqrtQam + re; // Este cálculo asume una constelación cuadrada simétrica
        } 


        private List<byte> ConvertIntToBits(int symbolValue, int bitsPerSymbol)
        {
            var bits = new List<byte>();
            for (int i = bitsPerSymbol - 1; i >= 0; i--)
            {
                bits.Add((byte)((symbolValue >> i) & 1));
            }
            return bits;
        }
        private byte[] BitsToBytes(List<byte> bits)
        {
            int byteCount = (bits.Count + 7) / 8; // Calcula el número de bytes necesario.
            byte[] bytes = new byte[byteCount];

            for (int i = 0; i < bits.Count; i++)
            {
                // Calcula el índice del byte al cual el bit actual pertenece.
                int byteIndex = i / 8;

                // Calcula la posición del bit dentro de su byte específico (0 para el bit más significativo).
                int bitPosition = 7 - (i % 8);

                // Establece el bit correspondiente en el byte.
                if (bits[i] == 1)
                {
                    bytes[byteIndex] |= (byte)(1 << bitPosition);
                }
            }

            return bytes;
        }
        public void SaveDemodulatedDataToCsv(byte[] demodulatedData, string filePath, int numberOfBytes)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Byte,Valor"); // Encabezados para las columnas CSV

                for (int i = 0; i < numberOfBytes && i < demodulatedData.Length; i++)
                {
                    // Escribir el índice del byte y su valor en formato CSV
                    writer.WriteLine($"{i},{demodulatedData[i]}");
                }
            }
        }

    }
    public class DataComparator
    {
        public static void CompareDataAndSaveReport(byte[] originalData, byte[] demodulatedData, string reportFilePath)
        {
            int minLength = Math.Min(originalData.Length, demodulatedData.Length);
            int maxLength = Math.Max(originalData.Length, demodulatedData.Length);
            int mismatches = 0;

            // Compara los bytes coincidentes en longitud.
            for (int i = 0; i < minLength; i++)
            {
                if (originalData[i] != demodulatedData[i])
                {
                    mismatches++;
                }
            }

            // Si los arreglos tienen diferentes longitudes, considera el exceso como discrepancias.
            mismatches += maxLength - minLength;

            // Calcula el porcentaje de coincidencia.
            double matchPercentage = ((double)(maxLength - mismatches) / maxLength) * 100;

            // Guarda el reporte en un archivo.
            using (var writer = new StreamWriter(reportFilePath))
            {
                writer.WriteLine("Reporte de Comparación de Datos");
                writer.WriteLine($"Longitud de Datos Originales: {originalData.Length}");
                writer.WriteLine($"Longitud de Datos Demodulados: {demodulatedData.Length}");
                writer.WriteLine($"Número de Discrepancias: {mismatches}");
                writer.WriteLine($"Porcentaje de Coincidencia: {matchPercentage:0.00}%");
            }
        }
    }

}


