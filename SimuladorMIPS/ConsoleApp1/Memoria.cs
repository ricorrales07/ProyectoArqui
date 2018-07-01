using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SimuladorMIPS
{
    class Memoria
    {
        // 24 bloques de datos y 40 de instrucciones.
        // Cada bloque de datos es de 4 enteros, y cada bloque de instrucciones es de 16.
        // 24 * 4 + 40 * 16 = 736
        private static readonly int size = 736;

        // Patrón singleton.
        private static Memoria instance = null;

        // Esto es un "Property" de C#.
        public static Memoria Instance
        {
            get
            {
                if (instance == null)
                    instance = new Memoria();
                return instance;
            }
        }

        private Memoria()
        {
            Mem = new int[size];
            for (int i = 0; i < size; i++)
            {
                Mem[i] = 1;
            }
            BusDeDatos = new Object();
            BusDeInstrucciones = new Object();
            Debug.Print("Memoria creada.");
        }

        // Retorna los contenidos de la memoria de forma que sea legible en la consola.
        public string PrettyPrint()
        {
            string output;

            output = "\tDatos:\n";

            // 6 filas, 4 columnas, 4 enteros por columna.
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        output += Mem[i * 16 + j * 4 + k] + " "; // YOLO.
                    }
                    output += "\t";
                }
                output += "\n";
            }

            output += "\n\tInstrucciones:\n";

            // 40 filas, 4 columnas, 4 enteros (una instrucción) por columna.
            for (int i = 0; i < 40; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        output += Mem[96 + i * 16 + j * 4 + k] + " "; // YOLO.
                    }
                    output += "\t";
                }
                output += "\n";
            }

            return output;
        }

        // Los buses podrían ser cualquier estructura de datos.
        // Lo único que nos interesa son los locks de estos objetos.
        // Usé bool para que tomen menos espacio.
        public Object BusDeDatos { get; set; }
        public Object BusDeInstrucciones { get; set; }

        public int[] Mem { get; set; }

        
    }
}
