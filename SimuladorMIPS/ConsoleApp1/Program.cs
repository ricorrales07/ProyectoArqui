using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;
using System.Diagnostics;
using System.IO;

// <>

// Ricardo: Tengo una idea: para evitar los "YOLO" a la hora de imprimir cosas,
// se podría bloquear los hilillos de los núcleos durante todo el tick de reloj
// y desbloquearlos al final. Luego, cuando se imprime, también se pide el lock.
// Esto evitaría problemas de acceso concurrente sin tener que estar pidiendo
// locks a cada rato.

namespace SimuladorMIPS
{
    class Program
    {
        static void Main(string[] args)
        {
            Memoria mem = Memoria.Instance;
            NucleoMultihilillo N0 = NucleoMultihilillo.Instance;
            NucleoMonohilillo N1 = NucleoMonohilillo.Instance;
            Queue<Hilillo> colaHilillos = new Queue<Hilillo>();
            List<Hilillo> hilillosFinalizados = new List<Hilillo>(); // WARNING: Colocar los hilillos finalizados en esta lista.
            Barrier barrera = new Barrier(3);
            int reloj = 0;

            // TIP: La clase Debug permite imprimir mensajes de debug en una consola distinta de la principal.
            Debug.Print("Asignando colas de hilillos y barrera a núcleos...");
            N0.ColaHilillos = colaHilillos;
            N1.ColaHilillos = colaHilillos;
            N0.Barrera = barrera;
            N1.Barrera = barrera;
            N0.HilillosFinalizados = hilillosFinalizados;
            N1.HilillosFinalizados = hilillosFinalizados;

            Debug.Print("Dando acceso a cada núcleo al otro.");
            N0.N1 = N1;
            N1.N0 = N0;

            // Solicitar al usuario hilillos a correr y cargarlos en memoria.
            int direccionDeInicioDeHilillo = 384; // Indica dónde comienza las instrucciones de cada hilillo.
            Console.WriteLine("Usted se encuentra en la carpeta " + Directory.GetCurrentDirectory());

            Console.WriteLine("Inserte el nombre de un archivo de hilillo o 'c' para continuar.");
            string nombreDeArchivo = Console.ReadLine();

            while (nombreDeArchivo != "c")
            {
                Hilillo h = new Hilillo(direccionDeInicioDeHilillo, nombreDeArchivo);

                StreamReader archivo;
                try
                {
                    archivo = new StreamReader(nombreDeArchivo);

                    int dir = 96 + (direccionDeInicioDeHilillo - 384);
                    while (!archivo.EndOfStream)
                    {
                        string instruccion = "";
                        try
                        {
                            instruccion = archivo.ReadLine();
                        }
                        catch (IOException e)
                        {
                            Console.WriteLine("Error al leer el archivo.");
                            break;
                        }
                        string[] temp = instruccion.Split(' ');
                        for (int i = 0; i < 4; i++)
                            mem.Mem[dir + i] = Convert.ToInt32(temp[i]);
                        dir += 4;
                    }

                    colaHilillos.Enqueue(h);
                    direccionDeInicioDeHilillo = 384 + (dir - 96);
                }
                catch (FileNotFoundException e)
                {
                    Console.WriteLine("No se encontró el archivo.");
                }

                Console.WriteLine("Inserte el nombre de un archivo hilillo o 'c' para continuar.");
                nombreDeArchivo = Console.ReadLine();
            }

            // Solicitar al usuario el quantum.
            Console.WriteLine("Inserte el quantum:");
            int quantum = Convert.ToInt32(Console.ReadLine());
            N0.Quantum = N1.Quantum = quantum;

            // Solicitar al usuario modalidad de ejecución (lenta/rápida).
            bool ejecucionLentaActivada = false;
            Console.WriteLine("¿Desea activar la modalidad de ejecución lenta? (s/n)");
            if (Console.ReadLine() == "s")
            {
                ejecucionLentaActivada = true;
                Console.WriteLine("Ejecución lenta activada.");
            }

            Debug.Print("Creando hilos de simulación...");
            Thread nucleo0 = new Thread(N0.Start);
            Thread nucleo1 = new Thread(N1.Start);

            // Esto echa a andar los hilos.
            nucleo0.Start();
            nucleo1.Start();

            Debug.Print("Hilo principal: Entrando a sección crítica: revisando si los núcleos terminaron...");
            Monitor.Enter(N0.Terminado);
            Monitor.Enter(N1.Terminado);
            while(!N0.Terminado || !N1.Terminado) // Sección crítica.
            {
                Monitor.Exit(N0.Terminado);
                Monitor.Exit(N1.Terminado);
                Debug.Print("Hilo principal: fin de sección crítica. Los núcleos no han terminado.");

                reloj++;

                Console.Clear();

                // Imprimir reloj.
                Console.WriteLine("Reloj: " + reloj);

                // Imprimir identificación de hilillos en ejecución.
                Console.WriteLine("Hilillos en ejecución:");
                Console.WriteLine("\tNúcleo 0:");
                Console.WriteLine(N0.PrettyPrintHilillos());

                Console.WriteLine("\tNúcleo 1:");
                Console.WriteLine(N1.PrettyPrintHilillos());

                if (ejecucionLentaActivada && reloj % 20 == 0)
                {
                    // Imprimir memoria, cachés y registros.
                    Console.WriteLine("Contenido de la memoria:");
                    Console.WriteLine(mem.PrettyPrint());

                    Console.WriteLine("Registros y cachés:");
                    Console.WriteLine("\tNúcleo 0:");
                    Console.WriteLine(N0.PrettyPrintRegistrosYCaches());

                    Console.WriteLine("\tNúcleo 1:");
                    Console.WriteLine(N1.PrettyPrintRegistrosYCaches());

                    Console.ReadKey();
                }

                // Pasar por la barrera.
                barrera.SignalAndWait();

                Debug.Print("Hilo principal: Entrando a sección crítica: revisando si los núcleos terminaron...");
                Monitor.Enter(N0.Terminado);
                Monitor.Enter(N1.Terminado);
            }
            Monitor.Exit(N0.Terminado);
            Monitor.Exit(N1.Terminado);
            Debug.Print("Hilo principal: fin de sección crítica. Los núcleos terminaron.");

            // Finalizar hilos y barrera.
            nucleo0.Abort(); // TODO: Verificar correcto funcionamiento de esta función.
            nucleo1.Abort();
            barrera.Dispose();

            Console.WriteLine("Fin de la simulación.\n");

            // Imprimir contenido de memoria y cachés.
            Console.WriteLine("Contenido de la memoria:");
            Console.WriteLine(mem.PrettyPrint());

            Console.WriteLine("Registros y cachés:");
            Console.WriteLine("\tNúcleo 0:");
            Console.WriteLine(N0.PrettyPrintRegistrosYCaches());

            Console.WriteLine("\tNúcleo 1:");
            Console.WriteLine(N1.PrettyPrintRegistrosYCaches());

            // Para cada hilillo que corrió, imprimir registros y ciclos que duró.
            Console.WriteLine("Hilillos que corrieron:");
            foreach (Hilillo h in hilillosFinalizados)
            {
                Console.WriteLine(h.PrettyPrintRegistrosYCiclos());
            }
        }
    }
}
