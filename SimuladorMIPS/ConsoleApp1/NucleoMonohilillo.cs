using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace SimuladorMIPS
{
    class NucleoMonohilillo
    {
        // Patrón singleton.
        private static NucleoMonohilillo instance = null;

        // TIP: Esto es un "Property" de C#.
        public static NucleoMonohilillo Instance
        {
            get
            {
                if (instance == null)
                    instance = new NucleoMonohilillo();
                return instance;
            }
        }


        private NucleoMonohilillo()
        {
            Terminado = false;
            CacheD = new CacheDatos(tamanoCache);
            CacheI = new CacheInstrucciones(tamanoCache);
            //busDeDatosReservado = busDeInstruccionesReservado = false;
            h = Hilillo.HililloVacio;
            Debug.Print("Núcleo 1 creado.");
        }

        // Carga un hilillo y ejecuta Run() en un ciclo infinito.
        public void Start()
        {
            lock (ColaHilillos)
            {
                if (ColaHilillos.Count > 0)
                {
                    h = ColaHilillos.Dequeue();
                    // TIP: Es útil usar asserts de Debug cuando pensamos un caso que "nunca pasa".
                    Debug.Assert(h.Fase == Hilillo.FaseDeHilillo.L); // Creo que debería estar listo, pues es el inicio de la simulación.
                }
                else
                {
                    h = Hilillo.HililloVacio;
                }
            }

            while (true)
            {
                Run();
            }
        }

        // Aquí va la lógica general: fetch, execute, missI, missD.
        private void Run()
        {
            if (h.Fase == Hilillo.FaseDeHilillo.L)
            {
                Fetch();
            }
            else if (h.Fase == Hilillo.FaseDeHilillo.IR)
            {
                Execute();
            }

            if (h.Fase == Hilillo.FaseDeHilillo.FI)
            {
                MissI();
            }

            if (h.Fase == Hilillo.FaseDeHilillo.FD)
            {
                MissD();
            }

            Tick();
        }

        private void Fetch()
        {
            throw new NotImplementedException();
        }

        private void Execute()
        {
            Debug.Print("Núcleo 1: Inicio de Execute().");

            int X, Y, Z, n;
            int direccionDeMemoria, bloqueDeMemoria, posicionEnCache, palabra;

            switch (h.IR.CodigoDeOperacion)
            {
                case CodOp.FIN:
                    Debug.Print("Núcleo 1: Instruccion FIN. Pasando a etapa Fin.");
                    h.Fase = Hilillo.FaseDeHilillo.Fin;
                    break;

                case CodOp.LW:
                    Debug.Print("Núcleo 1: Instruccion LW.");
                    Y = h.IR.Operando[0];
                    X = h.IR.Operando[1];
                    n = h.IR.Operando[2];

                    direccionDeMemoria = h.Registro[Y] + n;
                    bloqueDeMemoria = direccionDeMemoria / 16;
                    posicionEnCache = bloqueDeMemoria % tamanoCache;
                    palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

                    Debug.Print("Núcleo 1: LW. Revisando bloque " + bloqueDeMemoria
                        + " en posición de caché " + posicionEnCache + ".");

                    Debug.Assert(!CacheD.Reservado[posicionEnCache]);
                    if (Monitor.TryEnter(CacheD.NumBloque[posicionEnCache]))
                    {
                        Debug.Print("Núcleo 1: Se bloqueó la posición con éxito.");
                        if (CacheD.NumBloque[posicionEnCache] == bloqueDeMemoria && CacheD.Estado[posicionEnCache] != EstadoDeBloque.I)
                        {
                            Debug.Print("Núcleo 1: Bloque encontrado. Se puede leer sin problema.");
                            h.Registro[X] = CacheD.Cache[palabra, posicionEnCache];
                            Monitor.Exit(CacheD.NumBloque[posicionEnCache]);
                            h.Fase = Hilillo.FaseDeHilillo.Exec;
                            Debug.Print("Núcleo 1: LW ejecutado. Pasando a fase Exec...");
                        }
                        else // No es la que buscamos o está inválida.
                        {
                            Debug.Print("Núcleo 1: No se encontró el bloque en caché. Pasando a fase FD...");
                            Monitor.Exit(CacheD.NumBloque[posicionEnCache]);
                            h.Fase = Hilillo.FaseDeHilillo.FD;
                            h.Recursos = false;
                            h.EtapaDeSnooping = Hilillo.EtapaSnooping.ANTES;
                            Debug.Print("Núcleo 1: Fallo de caché detectado en Execute(). Fin del método.");
                        }
                    }
                    else
                    {
                        Debug.Print("Núcleo 1: No se pudo bloquear posición. Fin de Execute().");
                    }

                    break;

                case CodOp.SW:
                    Debug.Print("Núcleo 1: Instruccion SW.");
                    Y = h.IR.Operando[0];
                    X = h.IR.Operando[1];
                    n = h.IR.Operando[2];

                    direccionDeMemoria = h.Registro[Y] + n;
                    bloqueDeMemoria = direccionDeMemoria / 16;
                    posicionEnCache = bloqueDeMemoria % tamanoCache;
                    palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

                    Debug.Print("Núcleo 1: SW. Revisando bloque " + bloqueDeMemoria
                        + " en posición de caché " + posicionEnCache + ".");

                    Debug.Assert(!CacheD.Reservado[posicionEnCache]);
                    if (Monitor.TryEnter(CacheD.NumBloque[posicionEnCache]))
                    {
                        Debug.Print("Núcleo 1: Se bloqueó la posición con éxito.");
                        if (CacheD.NumBloque[posicionEnCache] == bloqueDeMemoria && CacheD.Estado[posicionEnCache] == EstadoDeBloque.M)
                        {
                            Debug.Print("Núcleo 1: Bloque encontrado. Se puede escribir sin problema.");
                            CacheD.Cache[palabra, posicionEnCache] = h.Registro[X];
                            Monitor.Exit(CacheD.NumBloque[posicionEnCache]);
                            h.Fase = Hilillo.FaseDeHilillo.Exec;
                            Debug.Print("Núcleo 1: SW ejecutado. Pasando a fase Exec...");
                        }
                        else // No es la que buscamos, está inválida o es el caso especial en el que está compartida.
                        {
                            Debug.Print("Núcleo 1: No se encontró el bloque en caché. Pasando a fase FD...");
                            Monitor.Exit(CacheD.NumBloque[posicionEnCache]);
                            h.Fase = Hilillo.FaseDeHilillo.FD;
                            h.Recursos = false;
                            h.EtapaDeSnooping = Hilillo.EtapaSnooping.ANTES;
                            Debug.Print("Núcleo 1: Fallo de caché detectado en Execute(). Fin del método.");
                        }
                    }
                    else
                    {
                        Debug.Print("Núcleo 1: No se pudo bloquear posición. Fin de Execute().");
                    }
                    break;

                case CodOp.DADDI:
                    Debug.Print("Núcleo 1: Instrucción DADDI.");
                    Y = h.IR.Operando[0];
                    X = h.IR.Operando[1];
                    n = h.IR.Operando[2];

                    h.Registro[X] = h.Registro[Y] + n;
                    Debug.Print("Núcleo 1: DADDI ejecutado. Fin de Execute().");

                    break;

                case CodOp.DADD:
                    Debug.Print("Núcleo 1: Instrucción DADD.");
                    Y = h.IR.Operando[0];
                    Z = h.IR.Operando[1];
                    X = h.IR.Operando[2];

                    h.Registro[X] = h.Registro[Y] + h.Registro[Z];
                    Debug.Print("Núcleo 1: DADD ejecutado. Fin de Execute().");

                    break;

                case CodOp.DSUB:
                    Debug.Print("Núcleo 1: Instrucción DSUB.");
                    Y = h.IR.Operando[0];
                    Z = h.IR.Operando[1];
                    X = h.IR.Operando[2];

                    h.Registro[X] = h.Registro[Y] - h.Registro[Z];
                    Debug.Print("Núcleo 1: DSUB ejecutado. Fin de Execute().");

                    break;

                case CodOp.DMUL:
                    Debug.Print("Núcleo 1: Instrucción DMUL.");
                    Y = h.IR.Operando[0];
                    Z = h.IR.Operando[1];
                    X = h.IR.Operando[2];

                    h.Registro[X] = h.Registro[Y] * h.Registro[Z];
                    Debug.Print("Núcleo 1: DMUL ejecutado. Fin de Execute().");

                    break;

                case CodOp.DDIV:
                    Debug.Print("Núcleo 1: Instrucción DDIV.");
                    Y = h.IR.Operando[0];
                    Z = h.IR.Operando[1];
                    X = h.IR.Operando[2];

                    h.Registro[X] = h.Registro[Y] / h.Registro[Z];
                    Debug.Print("Núcleo 1: DDIV ejecutado. Fin de Execute().");

                    break;

                case CodOp.BEQZ:
                    Debug.Print("Núcleo 1: Instrucción BEQZ.");
                    X = h.IR.Operando[0];
                    n = h.IR.Operando[2];

                    if (h.Registro[X] == 0)
                    {
                        Debug.Print("Núcleo 1: Registro " + X + " es cero. Saltando a direccion " + (h.PC + n * 4) + "...");
                        h.PC += n * 4;
                    }
                    else
                    {
                        Debug.Print("Núcleo 1: Registro " + X + " no es cero: " + h.Registro[X]);
                    }

                    Debug.Print("Núcleo 1: BEQZ ejecutado. Fin de Execute().");

                    break;

                case CodOp.BNEZ:
                    Debug.Print("Núcleo 1: Instrucción BNEZ.");
                    X = h.IR.Operando[0];
                    n = h.IR.Operando[2];

                    if (h.Registro[X] != 0)
                    {
                        Debug.Print("Núcleo 1: Registro " + X + " no es cero (" + h.Registro[X]
                            + "). Saltando a direccion " + (h.PC + n * 4) + "...");
                        h.PC += n * 4;
                    }
                    else
                    {
                        Debug.Print("Núcleo 1: Registro " + X + " sí es cero.");
                    }

                    Debug.Print("Núcleo 1: BNEZ ejecutado. Fin de Execute().");

                    break;

                case CodOp.JAL:
                    Debug.Print("Núcleo 1: Instrucción JAL.");
                    n = h.IR.Operando[2];

                    Debug.Print("Núcleo 1: Link a direccion: " + h.PC);
                    h.Registro[31] = h.PC;
                    h.PC += n * 4;

                    Debug.Print("Núcleo 1: JAL ejecutado. Fin de Execute().");

                    break;

                case CodOp.JR:
                    Debug.Print("Núcleo 1: Instrucción JR.");
                    X = h.IR.Operando[0];

                    Debug.Print("Núcleo 1: Saltando a direccion: " + h.Registro[X]);
                    h.PC = h.Registro[X];

                    Debug.Print("Núcleo 1: JR ejecutado. Fin de Execute().");

                    break;

                default:
                    Debug.Assert(false);
                    break;
            }
        }

        private void MissI()
        {
            throw new NotImplementedException();
        }

        private void MissD()
        {
            Debug.Assert(h.Fase == Hilillo.FaseDeHilillo.FD);

            int Y = h.IR.Operando[0];
            int X = h.IR.Operando[1];
            int n = h.IR.Operando[2];
            int direccionDeMemoria = h.Registro[Y] + n;
            int bloqueDeMemoria = direccionDeMemoria / 16;
            int posicionEnCache = bloqueDeMemoria % tamanoCache;
            int palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

            Debug.Print("Núcleo 1: Fallo de datos. Revisando bloque " + bloqueDeMemoria
                + " en posición de caché " + posicionEnCache + ".");

            if (!h.Recursos)
            {
                Debug.Print("Núcleo 1: Recursos no disponibles.");

                if (!Monitor.TryEnter(CacheD.NumBloque[posicionEnCache]))
                {
                    Debug.Print("Núcleo 1: No se pudo bloquear posición en caché. Fin de MissD().");
                    return;
                }
                if (!Monitor.TryEnter(Memoria.Instance.BusDeDatos))
                {
                    Monitor.Exit(CacheD.NumBloque[posicionEnCache]);
                    Debug.Print("Núcleo 1: No se pudo bloquear bus de datos. Fin de MissD().");
                    return;
                }

                Debug.Print("Núcleo 1: Se bloqueó la posición de caché y bus de datos.");

                h.Recursos = true;

                if (CacheD.Estado[posicionEnCache] == EstadoDeBloque.M)
                {
                    Debug.Print("Núcleo 1: Bloque modificado. Es necesario copiar en memoria.");
                    h.Ticks = 40;
                }
                else
                {
                    goto RevisarEtapaSnooping;
                }
            }
            else
            {
                Debug.Print("Núcleo 1: Recursos disponibles.");

                if (CacheD.Estado[posicionEnCache] != EstadoDeBloque.M)
                {
                    goto RevisarEtapaSnooping;
                }
            }

            h.Ticks--;
            if (h.Ticks > 0)
            {
                Debug.Print("Núcleo 1: \"Copiando\" bloque modificado en memoria. Ticks restantes: " + h.Ticks);
                return;
            }
            else
            {
                Debug.Assert(h.Ticks == 0);

                // Se copia a memoria el bloque modificado.
                Debug.Print("Núcleo 1: Copiando bloque de la posición de caché " + posicionEnCache
                    + " a dirección de memoria " + direccionDeMemoria + "(posición de memoria simulada: "
                    + (direccionDeMemoria / 4) + ").");
                for (int j = 0; j < 4; j++)
                {
                    Memoria.Instance.Mem[direccionDeMemoria / 4 + j] = CacheD.Cache[j, posicionEnCache];
                }

                CacheD.Estado[posicionEnCache] = EstadoDeBloque.I;
            }

            RevisarEtapaSnooping:
            Debug.Print("Núcleo 1: Revisando etapa de snooping...");
            int posicionEnCacheN0 = bloqueDeMemoria % NucleoMonohilillo.tamanoCache;
            switch (h.EtapaDeSnooping)
            {
                case Hilillo.EtapaSnooping.ANTES:
                    Debug.Print("Núcleo 1: Etapa de snooping: ANTES.");
                    if (!Monitor.TryEnter(N0.CacheD.NumBloque[posicionEnCacheN0]))
                    {
                        Debug.Print("Núcleo 1: No se pudo reservar la posición de caché en N0. Fin de missD().");
                        return;
                    }

                    Debug.Print("Núcleo 1: Posición de caché en N0 reservada.");

                    if (N0.CacheD.NumBloque[posicionEnCacheN0] != bloqueDeMemoria
                        || (N0.CacheD.NumBloque[posicionEnCacheN0] == bloqueDeMemoria
                            && N0.CacheD.Estado[posicionEnCacheN0] == EstadoDeBloque.I)) // ¿Es la que queremos?
                    {
                        // No.
                        Debug.Print("Núcleo 1: El bloque no está en N0.");
                        if (!(h.IR.CodigoDeOperacion == CodOp.SW && CacheD.Estado[posicionEnCache] == EstadoDeBloque.C))
                        {
                            Debug.Print("Núcleo 1: Se debe cargar dato desde memoria. Pasando a etapa \"cargar\"...");
                            h.EtapaDeSnooping = Hilillo.EtapaSnooping.CARGAR;
                            Monitor.Exit(N0.CacheD.NumBloque[posicionEnCacheN0]);
                            h.Ticks = 40;
                            goto case Hilillo.EtapaSnooping.CARGAR;
                        }
                        else
                        {
                            Debug.Print("Núcleo 1: No es necesario cargar el dato de memoria ni hacer nada en N0. Pasando a etapa \"después\"...");
                            Monitor.Exit(N0.CacheD.NumBloque[posicionEnCacheN0]);
                            h.EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                            goto case Hilillo.EtapaSnooping.DESPUES;
                        }
                    }
                    else
                    {
                        // Sí.
                        Debug.Print("Núcleo 1: El bloque sí está en N0.");
                        if (N0.CacheD.Estado[posicionEnCacheN0] == EstadoDeBloque.C)
                        {
                            Debug.Print("Núcleo 1: El bloque en N0 está compartido.");
                            if (h.IR.CodigoDeOperacion == CodOp.LW)
                            {
                                Debug.Print("Núcleo 1: La operación es un LW, por lo que no hay que hacer nada en N0.");
                                Debug.Print("Núcleo 1: Se debe cargar dato desde memoria. Pasando a etapa \"cargar\"...");
                                h.EtapaDeSnooping = Hilillo.EtapaSnooping.CARGAR;
                                Monitor.Exit(N0.CacheD.NumBloque[posicionEnCacheN0]);
                                h.Ticks = 40;
                                goto case Hilillo.EtapaSnooping.CARGAR;
                            }
                            else
                            {
                                Debug.Assert(h.IR.CodigoDeOperacion == CodOp.SW);
                                Debug.Print("Núcleo 1: La operación es un SW, por lo que invalidamos el bloque en N0.");
                                N0.CacheD.Estado[posicionEnCacheN0] = EstadoDeBloque.I;

                                if (CacheD.Estado[posicionEnCache] != EstadoDeBloque.C)
                                {
                                    Debug.Print("Núcleo 1: Se debe cargar dato desde memoria. Pasando a etapa \"cargar\"...");
                                    h.EtapaDeSnooping = Hilillo.EtapaSnooping.CARGAR;
                                    Monitor.Exit(N0.CacheD.NumBloque[posicionEnCacheN0]);
                                    h.Ticks = 40;
                                    goto case Hilillo.EtapaSnooping.CARGAR;
                                }
                                else
                                {
                                    Debug.Assert(CacheD.Estado[posicionEnCache] == EstadoDeBloque.I);
                                    Debug.Print("Núcleo 1: Caso especial de SW con bloque en C."
                                        + " No es necesario cargar el dato de memoria. Pasando a etapa \"después\"...");
                                    Monitor.Exit(N0.CacheD.NumBloque[posicionEnCacheN0]);
                                    h.EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                                    goto case Hilillo.EtapaSnooping.DESPUES;
                                }
                            }
                        }
                        else
                        {
                            Debug.Assert(N0.CacheD.Estado[posicionEnCacheN0] == EstadoDeBloque.M);
                            Debug.Print("Núcleo 1: El bloque en N0 está modificado; es necesario copiarlo a memoria y a N1."
                                + " Pasando a etapa \"durante\"...");

                            h.EtapaDeSnooping = Hilillo.EtapaSnooping.DURANTE;
                            h.Ticks = 40;
                            goto case Hilillo.EtapaSnooping.DURANTE;
                        }
                    }
                case Hilillo.EtapaSnooping.DURANTE:
                    Debug.Print("Núcleo 1: Etapa snooping: DURANTE.");
                    h.Ticks--;
                    if (h.Ticks > 0)
                    {
                        Debug.Print("Núcleo 1: \"Copiando\" bloque de N0 a memoria y N1. Ticks restantes: " + h.Ticks);
                        return;
                    }
                    else
                    {
                        Debug.Assert(h.Ticks == 0);
                        Debug.Print("Núcleo 1: Copiando bloque de la posición de caché " + posicionEnCacheN0
                            + " (en N0) a dirección de memoria " + direccionDeMemoria + "(posición de memoria simulada: "
                            + (direccionDeMemoria / 4) + ") y a la posición de caché " + posicionEnCache + " en N1.");

                        for (int j = 0; j < 4; j++)
                        {
                            CacheD.Cache[j, posicionEnCache] = Memoria.Instance.Mem[direccionDeMemoria / 4 + j] = N0.CacheD.Cache[j, posicionEnCacheN0];
                        }

                        if (h.IR.CodigoDeOperacion == CodOp.LW)
                        {
                            Debug.Print("Núcleo 1: La instrucción es un LW; el bloque en N0 queda en C.");
                            N0.CacheD.Estado[posicionEnCacheN0] = EstadoDeBloque.C;
                        }
                        else
                        {
                            Debug.Assert(h.IR.CodigoDeOperacion == CodOp.SW);
                            Debug.Print("Núcleo 1: La instrucción es un SW; el bloque en N0 queda en I.");
                            N0.CacheD.Estado[posicionEnCacheN0] = EstadoDeBloque.I;
                        }

                        Monitor.Exit(N0.CacheD.NumBloque[posicionEnCacheN0]);

                        h.EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                        goto case Hilillo.EtapaSnooping.DESPUES;
                    }

                case Hilillo.EtapaSnooping.CARGAR:
                    Debug.Print("Núcleo 1: Etapa snooping: CARGAR.");
                    h.Ticks--;
                    if (h.Ticks > 0)
                    {
                        Debug.Print("Núcleo 1: \"Copiando\" bloque de memoria a N1. Ticks restantes: " + h.Ticks);
                        return;
                    }
                    else
                    {
                        Debug.Assert(h.Ticks == 0);
                        Debug.Print("Núcleo 1: Copiando bloque de la dirección de memoria "
                            + direccionDeMemoria + "(posición de memoria simulada: "
                            + (direccionDeMemoria / 4) + ") a la posición de caché " + posicionEnCache + " en N1.");

                        for (int j = 0; j < 4; j++)
                        {
                            CacheD.Cache[j, posicionEnCache] = Memoria.Instance.Mem[direccionDeMemoria / 4 + j];
                        }

                        h.EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                        goto case Hilillo.EtapaSnooping.DESPUES;
                    }

                case Hilillo.EtapaSnooping.DESPUES:
                    Debug.Print("Núcleo 1: Etapa snooping: DESPUES.");

                    Debug.Print("Núcleo 1: Terminamos de usar el bus de datos. Se libera.");
                    Monitor.Exit(Memoria.Instance.BusDeDatos);

                    if (h.IR.CodigoDeOperacion == CodOp.LW)
                    {
                        Debug.Print("Núcleo 1: La operación es un LW, se copia de posición en caché " + posicionEnCache
                            + ", palabra " + palabra + ", a registro " + X + ". El bloque queda compartido.");
                        h.Registro[X] = CacheD.Cache[palabra, posicionEnCache];
                        CacheD.Estado[posicionEnCache] = EstadoDeBloque.C;
                    }
                    else
                    {
                        Debug.Assert(h.IR.CodigoDeOperacion == CodOp.SW);
                        Debug.Print("Núcleo 1: La operación es un SW, se copia de registro " + X + " a posición en caché "
                            + posicionEnCache + ", palabra " + palabra + ". El bloque queda modificado.");
                        CacheD.Cache[palabra, posicionEnCache] = h.Registro[X];
                        CacheD.Estado[posicionEnCache] = EstadoDeBloque.M;
                    }

                    Debug.Print("Núcleo 1: Terminamos de usar la caché. Se libera.");
                    Monitor.Exit(CacheD.NumBloque[posicionEnCache]);

                    // Las reservas deberían ser innecesarias en el núcleo 1.
                    //CacheD.Reservado[posicionEnCache] = false;
                    //busDeDatosReservado = false;

                    h.Fase = Hilillo.FaseDeHilillo.Exec;
                    return;
            }
        }

        private void Tick()
        {
            throw new NotImplementedException();
        }

        // Retorna información general de los hilillos que están corriendo para desplegarla en pantalla durante la ejecución.
        public string PrettyPrintHilillos()
        {
            string output = "\t\tHilillo 0: " + h.Nombre; // YOLO.

            return output;
        }

        // Retorna los contenidos de los registros y las cachés, de forma legible en consola.
        public string PrettyPrintRegistrosYCaches()
        {
            string output = "";

            output += "\t\tRegistros: \n"
                + "\t\t\tHilillo 0:\n"
                + h.PrettyPrintRegistrosYCiclos()
                + "\t\tCachés:\n"
                + "\t\t\tCaché de instrucciones:\n\n";

            for (int i = 0; i < tamanoCache; i++)
            {
                output += "Posición " + i + "\t";
            }
            output += "\n";

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < tamanoCache; j++)
                {
                    output += CacheI.Cache[i, j].CodigoDeOperacion + " ";
                    for (int k = 0; k < 3; k++)
                    {
                        output += CacheI.Cache[i, j].Operando[k] + " ";
                    }
                    output += "\t";
                }
                output += "\n";
            }

            for (int i = 0; i < tamanoCache; i++)
            {
                output += CacheI.NumBloque[i] + "\t";
            }

            output += "\n\n";

            output += "\t\t\tCaché de datos:\n\n";

            for (int i = 0; i < tamanoCache; i++)
            {
                output += "Posición " + i + "\t";
            }
            output += "\n";

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < tamanoCache; j++)
                {
                    output += CacheD.Cache[i, j] + "\t";
                }
                output += "\n";
            }

            for (int i = 0; i < tamanoCache; i++)
            {
                output += CacheD.NumBloque[i] + "\t";
            }

            for (int i = 0; i < tamanoCache; i++)
            {
                output += CacheD.Estado[i] + "\t";
            }

            return output;
        }

        public Queue<Hilillo> ColaHilillos { get; set; }
        public bool Terminado { get; set; }
        public int Quantum { get; set; }
        public Barrier Barrera { get; set; }
        public List<Hilillo> HilillosFinalizados { get; set; }
        public bool Cancelado { get; set; }

        private Hilillo h;

        public CacheDatos CacheD { get; set; }
        private CacheInstrucciones CacheI; // Miembro privado, porque nadie va a acceder a ella desde fuera.
        public const int tamanoCache = 4;

        //private bool busDeDatosReservado;
        //private bool busDeInstruccionesReservado;

        public NucleoMultihilillo N0 { get; set; }
    }
}
