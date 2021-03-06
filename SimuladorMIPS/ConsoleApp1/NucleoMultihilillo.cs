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
    class NucleoMultihilillo
    {
        // Patrón singleton.
        private static NucleoMultihilillo instance = null;

        // TIP: Esto es un "Property" de C#.
        public static NucleoMultihilillo Instance
        {
            get
            {
                if (instance == null)
                    instance = new NucleoMultihilillo();
                return instance;
            }
        }


        private NucleoMultihilillo()
        {
            Terminado = false;
            CacheD = new CacheDatos(tamanoCache);
            CacheI = new CacheInstrucciones(tamanoCache);
            busDeDatosReservado = busDeInstruccionesReservado = false;
            h = new Hilillo[] { Hilillo.HililloVacio, Hilillo.HililloVacio };
            Debug.Print("Núcleo 0 creado.");
        }

        // Carga un hilillo en H0 y ejecuta Run() en un ciclo infinito.
        public void Start()
        {
            lock (ColaHilillos)
            {
                if (ColaHilillos.Count > 0)
                {
                    h[0] = ColaHilillos.Dequeue();
                    h[0].Quantum = this.Quantum;
                    // TIP: Es útil usar asserts de Debug cuando pensamos un caso que "nunca pasa".
                    Debug.Assert(h[0].Fase == Hilillo.FaseDeHilillo.L); // Creo que debería estar listo, pues es el inicio de la simulación.
                }
                else
                {
                    h[0] = Hilillo.HililloVacio;
                }
            }
            h[1] = Hilillo.HililloVacio;

            while (true)
            {
                Run();
            }
        }

        // Aquí va la lógica general: fetch, execute, missI, missD.
        private void Run()
        {
            if (h[0].Fase == Hilillo.FaseDeHilillo.L)
            {
                Fetch(0);
            }
            else if (h[0].Fase == Hilillo.FaseDeHilillo.IR)
            {
                Execute(0);
            }
            else if (h[1].Fase == Hilillo.FaseDeHilillo.L)
            {
                Fetch(1);
            }
            else if(h[1].Fase == Hilillo.FaseDeHilillo.IR)
            {
                Execute(1);
            }

            Debug.Assert(!(h[0].Fase == Hilillo.FaseDeHilillo.FI && h[1].Fase == Hilillo.FaseDeHilillo.FI));
            if (h[0].Fase == Hilillo.FaseDeHilillo.FI)
            {
                MissI(0);
            }
            else if (h[1].Fase == Hilillo.FaseDeHilillo.FI)
            {
                MissI(1);
            }

            Debug.Assert(!(h[0].Fase == Hilillo.FaseDeHilillo.FD && h[1].Fase == Hilillo.FaseDeHilillo.FD));
            if (h[0].Fase == Hilillo.FaseDeHilillo.FD)
            {
                MissD(0);
            }
            else if (h[1].Fase == Hilillo.FaseDeHilillo.FD)
            {
                MissD(1);
            }

            Tick();
        }

        // i: número del hilillo que va a hacer el fetch.
        private void Fetch(int i)
        {
            Debug.Print("Núcleo 0: Inicio de Fetch().");

            int direccionDeMemoria, bloqueDeMemoria, posicionEnCache, palabra;

            //calcular número de bloque
            direccionDeMemoria = h[i].PC;
            bloqueDeMemoria = direccionDeMemoria / 16;
            posicionEnCache = bloqueDeMemoria % tamanoCache;
            palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

            Debug.Print("Núcleo 0: Fetch(). Revisando bloque " + bloqueDeMemoria
                        + " en posición de caché " + posicionEnCache + ".");

            if (!CacheI.Reservado[posicionEnCache])
            { //caché no está reservado
                Debug.Print("Núcleo 0: Fetch(). Posición de caché no reservada. Revisando caché...");
                if (CacheI.NumBloque[posicionEnCache] == bloqueDeMemoria)
                { //es el bloque que queremos
                    h[i].IR = new Instruccion(CacheI.Cache[palabra, posicionEnCache]);
                    h[i].PC += 4;
                    h[i].Fase = Hilillo.FaseDeHilillo.IR;
                    Debug.Print("Núcleo 0 Fetch(): Se encontró el bloque en caché. Pasando a fase IR...");
                    Execute(i);
                }
                else
                { //no es el bloque que queremos
                    if(!busDeInstruccionesReservado)
                    {
                        Debug.Print("Núcleo 0: Fetch(). Bus de instrucciones no reservado. Tomando bus...");
                        CacheI.Reservado[posicionEnCache] = true;
                        busDeInstruccionesReservado = true;
                        h[i].Recursos = false;
                        h[i].Fase = Hilillo.FaseDeHilillo.FI;
                        Debug.Print("Núcleo 0: No se encontró el bloque en caché. Pasando a fase FI...");
                    }
                }
            }
        }

        // i: número del hilillo que se va a ejecutar.
        private void Execute(int i)
        {
            Debug.Print("Núcleo 0: Inicio de Execute().");

            int X, Y, Z, n;
            int direccionDeMemoria, bloqueDeMemoria, posicionEnCache, palabra;

            switch (h[i].IR.CodigoDeOperacion)
            {
                case CodOp.FIN:
                    Debug.Print("Núcleo 0: Instruccion FIN. Pasando a etapa Fin.");
                    h[i].Fase = Hilillo.FaseDeHilillo.Fin;
                    break;

                case CodOp.LW:
                    Debug.Print("Núcleo 0: Instruccion LW.");
                    Y = h[i].IR.Operando[0];
                    X = h[i].IR.Operando[1];
                    n = h[i].IR.Operando[2];

                    direccionDeMemoria = h[i].Registro[Y] + n;
                    bloqueDeMemoria = direccionDeMemoria / 16;
                    posicionEnCache = bloqueDeMemoria % tamanoCache;
                    palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

                    Debug.Print("Núcleo 0: LW. Revisando bloque " + bloqueDeMemoria
                        + " en posición de caché " + posicionEnCache + " para hilillo " + i + ".");

                    if (!CacheD.Reservado[posicionEnCache])
                    {
                        Debug.Print("Núcleo 0: La posición no está reservada.");
                        if (Monitor.TryEnter(CacheD.Lock[posicionEnCache]))
                        {
                            Debug.Print("Núcleo 0: Se bloqueó la posición con éxito.");
                            if (CacheD.NumBloque[posicionEnCache] == bloqueDeMemoria && CacheD.Estado[posicionEnCache] != EstadoDeBloque.I)
                            {
                                Debug.Print("Núcleo 0: Bloque encontrado. Se puede leer sin problema.");
                                h[i].Registro[X] = CacheD.Cache[palabra, posicionEnCache];
                                Monitor.Exit(CacheD.Lock[posicionEnCache]);
                                h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                                Debug.Print("Núcleo 0: LW ejecutado. Pasando a fase Exec...");
                            }
                            else // No es la que buscamos o está inválida.
                            {
                                Debug.Print("Núcleo 0: No se encontró el bloque en caché.");
                                if (!busDeDatosReservado)
                                {
                                    Debug.Print("Núcleo 0: El bus de datos no está reservado. Pasando a fase FD...");
                                    busDeDatosReservado = true;
                                    CacheD.Reservado[posicionEnCache] = true;

                                    Monitor.Exit(CacheD.Lock[posicionEnCache]);
                                    h[i].Fase = Hilillo.FaseDeHilillo.FD;
                                    h[i].Recursos = false;
                                    h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.ANTES;
                                    Debug.Print("Núcleo 0: Fallo de caché detectado en Execute(). Fin del método.");
                                }
                                else
                                {
                                    Monitor.Exit(CacheD.Lock[posicionEnCache]);
                                    Debug.Print("Núcleo 0: Bus de datos reservado. Fin de Execute().");
                                }
                            }
                        }
                        else
                        {
                            Debug.Print("Núcleo 0: No se pudo bloquear posición. Fin de Execute().");
                        }
                    }
                    else
                    {
                        Debug.Print("Núcleo 0: Posición reservada. Fin de Execute().");
                    }
                    break;

                case CodOp.SW:
                    Debug.Print("Núcleo 0: Instruccion SW.");
                    Y = h[i].IR.Operando[0];
                    X = h[i].IR.Operando[1];
                    n = h[i].IR.Operando[2];

                    direccionDeMemoria = h[i].Registro[Y] + n;
                    bloqueDeMemoria = direccionDeMemoria / 16;
                    posicionEnCache = bloqueDeMemoria % tamanoCache;
                    palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

                    Debug.Print("Núcleo 0: SW. Revisando bloque " + bloqueDeMemoria
                        + " en posición de caché " + posicionEnCache + " para hilillo " + i + ".");

                    if (!CacheD.Reservado[posicionEnCache])
                    {
                        Debug.Print("Núcleo 0: La posición no está reservada.");
                        if (Monitor.TryEnter(CacheD.Lock[posicionEnCache]))
                        {
                            Debug.Print("Núcleo 0: Se bloqueó la posición con éxito.");
                            if (CacheD.NumBloque[posicionEnCache] == bloqueDeMemoria && CacheD.Estado[posicionEnCache] == EstadoDeBloque.M)
                            {
                                Debug.Print("Núcleo 0: Bloque encontrado. Se puede escribir sin problema.");
                                CacheD.Cache[palabra, posicionEnCache] = h[i].Registro[X];
                                Monitor.Exit(CacheD.Lock[posicionEnCache]);
                                h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                                Debug.Print("Núcleo 0: SW ejecutado. Pasando a fase Exec...");
                            }
                            else // No es la que buscamos, está inválida o es el caso especial en el que está compartida.
                            {
                                Debug.Print("Núcleo 0: No se encontró el bloque en caché.");
                                if (!busDeDatosReservado)
                                {
                                    Debug.Print("Núcleo 0: El bus de datos no está reservado. Pasando a fase FD...");
                                    busDeDatosReservado = true;
                                    CacheD.Reservado[posicionEnCache] = true;

                                    Monitor.Exit(CacheD.Lock[posicionEnCache]);
                                    h[i].Fase = Hilillo.FaseDeHilillo.FD;
                                    h[i].Recursos = false;
                                    h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.ANTES;
                                    Debug.Print("Núcleo 0: Fallo de caché detectado en Execute(). Fin del método.");
                                }
                                else
                                {
                                    Monitor.Exit(CacheD.Lock[posicionEnCache]);
                                    Debug.Print("Núcleo 0: Bus de datos reservado. Fin de Execute().");
                                }
                            }
                        }
                        else
                        {
                            Debug.Print("Núcleo 0: No se pudo bloquear posición. Fin de Execute().");
                        }
                    }
                    else
                    {
                        Debug.Print("Núcleo 0: Posición reservada. Fin de Execute().");
                    }
                    break;

                case CodOp.DADDI:
                    Debug.Print("Núcleo 0: Instrucción DADDI.");
                    Y = h[i].IR.Operando[0];
                    X = h[i].IR.Operando[1];
                    n = h[i].IR.Operando[2];

                    Debug.Print("Escribiendo " + (h[i].Registro[Y] + n) + " en R" + X);

                    h[i].Registro[X] = h[i].Registro[Y] + n;
                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: DADDI ejecutado. Fin de Execute().");

                    break;

                case CodOp.DADD:
                    Debug.Print("Núcleo 0: Instrucción DADD.");
                    Y = h[i].IR.Operando[0];
                    Z = h[i].IR.Operando[1];
                    X = h[i].IR.Operando[2];

                    h[i].Registro[X] = h[i].Registro[Y] + h[i].Registro[Z];
                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: DADD ejecutado. Fin de Execute().");

                    break;

                case CodOp.DSUB:
                    Debug.Print("Núcleo 0: Instrucción DSUB.");
                    Y = h[i].IR.Operando[0];
                    Z = h[i].IR.Operando[1];
                    X = h[i].IR.Operando[2];

                    h[i].Registro[X] = h[i].Registro[Y] - h[i].Registro[Z];
                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: DSUB ejecutado. Fin de Execute().");

                    break;

                case CodOp.DMUL:
                    Debug.Print("Núcleo 0: Instrucción DMUL.");
                    Y = h[i].IR.Operando[0];
                    Z = h[i].IR.Operando[1];
                    X = h[i].IR.Operando[2];

                    h[i].Registro[X] = h[i].Registro[Y] * h[i].Registro[Z];
                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: DMUL ejecutado. Fin de Execute().");

                    break;

                case CodOp.DDIV:
                    Debug.Print("Núcleo 0: Instrucción DDIV.");
                    Y = h[i].IR.Operando[0];
                    Z = h[i].IR.Operando[1];
                    X = h[i].IR.Operando[2];

                    h[i].Registro[X] = h[i].Registro[Y] / h[i].Registro[Z];
                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: DDIV ejecutado. Fin de Execute().");

                    break;

                case CodOp.BEQZ:
                    Debug.Print("Núcleo 0: Instrucción BEQZ.");
                    X = h[i].IR.Operando[0];
                    n = h[i].IR.Operando[2];

                    if (h[i].Registro[X] == 0)
                    {
                        Debug.Print("Núcleo 0: Registro " + X + " es cero. Saltando a direccion " + (h[i].PC + n * 4) + "...");
                        h[i].PC += n * 4;
                    }
                    else
                    {
                        Debug.Print("Núcleo 0: Registro " + X + " no es cero: " + h[i].Registro[X]);
                    }

                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: BEQZ ejecutado. Fin de Execute().");

                    break;

                case CodOp.BNEZ:
                    Debug.Print("Núcleo 0: Instrucción BNEZ.");
                    X = h[i].IR.Operando[0];
                    n = h[i].IR.Operando[2];

                    if (h[i].Registro[X] != 0)
                    {
                        Debug.Print("Núcleo 0: Registro " + X + " no es cero (" + h[i].Registro[X] 
                            + "). Saltando a direccion " + (h[i].PC + n * 4) + "...");
                        h[i].PC += n * 4;
                    }
                    else
                    {
                        Debug.Print("Núcleo 0: Registro " + X + " sí es cero.");
                    }

                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: BNEZ ejecutado. Fin de Execute().");

                    break;

                case CodOp.JAL:
                    Debug.Print("Núcleo 0: Instrucción JAL.");
                    n = h[i].IR.Operando[2];

                    Debug.Print("Núcleo 0: Link a direccion: " + h[i].PC);
                    h[i].Registro[31] = h[i].PC;
                    h[i].PC += n;

                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: JAL ejecutado. Fin de Execute().");

                    break;

                case CodOp.JR:
                    Debug.Print("Núcleo 0: Instrucción JR.");
                    X = h[i].IR.Operando[0];

                    Debug.Print("Núcleo 0: Saltando a direccion: " + h[i].Registro[X]);
                    h[i].PC = h[i].Registro[X];

                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    Debug.Print("Núcleo 0: JR ejecutado. Fin de Execute().");

                    break;

                default:
                    Debug.Assert(false);
                    break;
            }
        }

        // i: número del hilillo del cual se va a manejar el fallo.
        private void MissI(int i)
        {
            Debug.Print("Núcleo 0: Comenzando método MissI.");
            Debug.Assert(h[i].Fase == Hilillo.FaseDeHilillo.FI);

            int direccionDeMemoria = h[i].PC;
            int bloqueDeMemoria = direccionDeMemoria / 16;
            int posicionEnCache = bloqueDeMemoria % tamanoCache;
            int palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

            Debug.Print("Núcleo 0: Fallo de instrucciones. Revisando bloque " + bloqueDeMemoria
                + " en posición de caché " + posicionEnCache + " para hilillo " + i + ".");

            if (!h[i].Recursos)
            {
                Debug.Print("Núcleo 0: Recursos no disponibles.");
                
                if (!Monitor.TryEnter(Memoria.Instance.BusDeInstrucciones))
                {
                    Debug.Print("Núcleo 0: No se pudo bloquear bus de instrucciones. Fin de MissI().");
                    return;
                }

                Debug.Print("Núcleo 0: Se bloqueó el bus de instrucciones.");

                h[i].Recursos = true;
                h[i].Ticks = 40;
            }

            h[i].Ticks--;
            if (h[i].Ticks > 0)
            {
                Debug.Print("Núcleo 0: Ticks restantes: " + h[i].Ticks);
                return;
            }
            else
            {
                Debug.Assert(h[i].Ticks == 0);
                int direccionDeMemoriaSimulada = bloqueDeMemoria * 16 - 384 + 96;
                Debug.Print("Núcleo 0: Copiando bloque de la dirección de memoria "
                    + (bloqueDeMemoria * 16) + " (posición de memoria simulada: "
                    + direccionDeMemoriaSimulada + ") a la posición de caché " + posicionEnCache + " en N0.");

                for (int j = 0; j < 4; j++)
                {
                    CacheI.Cache[j, posicionEnCache].CodigoDeOperacion = (CodOp)Memoria.Instance.Mem[direccionDeMemoriaSimulada];
                    for (int c = 0; c < 3; c++)
                    {
                        CacheI.Cache[j, posicionEnCache].Operando[c] = Memoria.Instance.Mem[direccionDeMemoriaSimulada + 1 + c];
                    }
                    direccionDeMemoriaSimulada += 4;
                    Debug.Print("Núcleo 0: Se acaba de cargar instrucción en caché: " + CacheI.Cache[j, posicionEnCache].CodigoDeOperacion
                        + " " + CacheI.Cache[j, posicionEnCache].Operando[0] + " " + CacheI.Cache[j, posicionEnCache].Operando[1]
                        + " " + CacheI.Cache[j, posicionEnCache].Operando[2]);
                    Debug.Print("Núcleo 0: En h[0] el IR es: " + h[0].IR.CodigoDeOperacion + " " + h[0].IR.Operando[0] + " " 
                        + h[0].IR.Operando[1] + " " + h[0].IR.Operando[2]);
                    Debug.Print("Núcleo 0: En h[1] el IR es: " + h[1].IR.CodigoDeOperacion + " " + h[1].IR.Operando[0] + " "
                        + h[1].IR.Operando[1] + " " + h[1].IR.Operando[2]);
                }
                CacheI.NumBloque[posicionEnCache] = bloqueDeMemoria;
                
                Monitor.Exit(Memoria.Instance.BusDeInstrucciones);
                h[i].IR = new Instruccion(CacheI.Cache[palabra, posicionEnCache]); //carga el IR
                h[i].PC += 4;
                CacheI.Reservado[posicionEnCache] = false; 
                busDeInstruccionesReservado = false;
                h[i].Fase = Hilillo.FaseDeHilillo.IR;

                Debug.Print("Núcleo 0: Fin de MissI().");
                return;
            }

        }

        // i: número del hilillo del cual se va a manejar el fallo.
        private void MissD(int i)
        {
            Debug.Assert(h[i].Fase == Hilillo.FaseDeHilillo.FD);

            int Y = h[i].IR.Operando[0];
            int X = h[i].IR.Operando[1];
            int n = h[i].IR.Operando[2];
            int direccionDeMemoria = h[i].Registro[Y] + n;
            int bloqueDeMemoria = direccionDeMemoria / 16;
            int posicionEnCache = bloqueDeMemoria % tamanoCache;
            int palabra = (direccionDeMemoria - bloqueDeMemoria * 16) / 4;

            Debug.Print("Núcleo 0: Fallo de datos. Revisando bloque " + bloqueDeMemoria
                + " en posición de caché " + posicionEnCache + " para hilillo " + i + " (encontrado bloque: " + CacheD.NumBloque[posicionEnCache]
                + ", estado: " + CacheD.Estado[posicionEnCache] + ").");

            Debug.Print("Núcleo 0: Y: " + Y + ", Registro[Y]: " + h[i].Registro[Y] + ", n: " + n);

            if (!h[i].Recursos)
            {
                Debug.Print("Núcleo 0: Recursos no disponibles.");

                if (!Monitor.TryEnter(CacheD.Lock[posicionEnCache]))
                {
                    Debug.Print("Núcleo 0: No se pudo bloquear posición en caché. Fin de MissD().");
                    return;
                }
                if (!Monitor.TryEnter(Memoria.Instance.BusDeDatos))
                {
                    Monitor.Exit(CacheD.Lock[posicionEnCache]);
                    Debug.Print("Núcleo 0: No se pudo bloquear bus de datos. Fin de MissD().");
                    return;
                }

                Debug.Print("Núcleo 0: Se bloqueó la posición de caché y bus de datos.");

                h[i].Recursos = true;

                if (CacheD.Estado[posicionEnCache] == EstadoDeBloque.M)
                {
                    Debug.Print("Núcleo 0: Bloque modificado. Es necesario copiar en memoria.");
                    h[i].Ticks = 40;
                }
                else
                {
                    goto RevisarEtapaSnooping;
                }
            }
            else
            {
                Debug.Print("Núcleo 0: Recursos disponibles.");
                Debug.Assert(Monitor.IsEntered(CacheD.Lock[posicionEnCache]));
                Debug.Assert(Monitor.IsEntered(Memoria.Instance.BusDeDatos));

                if (CacheD.Estado[posicionEnCache] != EstadoDeBloque.M)
                {
                    goto RevisarEtapaSnooping;
                }
            }

            h[i].Ticks--;
            if (h[i].Ticks > 0)
            {
                Debug.Print("Núcleo 0: \"Copiando\" bloque modificado en memoria. Ticks restantes: " + h[i].Ticks);
                return;
            }
            else
            {
                Debug.Assert(h[i].Ticks == 0);

                // Se copia a memoria el bloque modificado.
                int numBloqueModificado = CacheD.NumBloque[posicionEnCache];
                Debug.Print("Núcleo 0: Copiando bloque de la posición de caché " + posicionEnCache
                    + " a dirección de memoria " + (numBloqueModificado * 16) + " (posición de memoria simulada: "
                    + (numBloqueModificado * 4) + ").");
                for (int j = 0; j < 4; j++)
                {
                    Memoria.Instance.Mem[numBloqueModificado * 4 + j] = CacheD.Cache[j, posicionEnCache];
                }

                CacheD.Estado[posicionEnCache] = EstadoDeBloque.I;
            }

            RevisarEtapaSnooping:
            Debug.Print("Núcleo 0: Revisando etapa de snooping...");
            int posicionEnCacheN1 = bloqueDeMemoria % NucleoMonohilillo.tamanoCache;
            switch(h[i].EtapaDeSnooping)
            {
                case Hilillo.EtapaSnooping.ANTES:
                    Debug.Print("Núcleo 0: Etapa de snooping: ANTES.");
                    if (!Monitor.TryEnter(N1.CacheD.Lock[posicionEnCacheN1]))
                    {
                        Debug.Print("Núcleo 0: No se pudo reservar la posición de caché en N1. Fin de missD().");
                        return;
                    }

                    Debug.Print("Núcleo 0: Posición de caché en N1 reservada.");

                    if (N1.CacheD.NumBloque[posicionEnCacheN1] != bloqueDeMemoria 
                        || (N1.CacheD.NumBloque[posicionEnCacheN1] == bloqueDeMemoria 
                            && N1.CacheD.Estado[posicionEnCacheN1] == EstadoDeBloque.I)) // ¿Es la que queremos?
                    {
                        // No.
                        Debug.Print("Núcleo 1: El bloque no está en N1 (posicionEnCacheN1: " + posicionEnCacheN1
                            + ", numBloque: " + N1.CacheD.NumBloque[posicionEnCacheN1]
                            + ", Estado: " + N1.CacheD.Estado[posicionEnCacheN1] + ").");
                        if (!(h[i].IR.CodigoDeOperacion == CodOp.SW && CacheD.NumBloque[posicionEnCache] == bloqueDeMemoria &&
                            CacheD.Estado[posicionEnCache] == EstadoDeBloque.C))
                        {
                            Debug.Print("Núcleo 0: Se debe cargar dato desde memoria. Pasando a etapa \"cargar\"...");
                            h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.CARGAR;
                            Monitor.Exit(N1.CacheD.Lock[posicionEnCacheN1]);
                            h[i].Ticks = 40;
                            goto case Hilillo.EtapaSnooping.CARGAR;
                        }
                        else
                        {
                            Debug.Print("Núcleo 0: No es necesario cargar el dato de memoria ni hacer nada en N1. Pasando a etapa \"después\"...");
                            Monitor.Exit(N1.CacheD.Lock[posicionEnCacheN1]);
                            h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                            goto case Hilillo.EtapaSnooping.DESPUES;
                        }
                    }
                    else
                    {
                        // Sí.
                        Debug.Print("Núcleo 0: El bloque sí está en N1.");
                        if (N1.CacheD.Estado[posicionEnCacheN1] == EstadoDeBloque.C)
                        {
                            Debug.Print("Núcleo 0: El bloque en N1 está compartido.");
                            if (h[i].IR.CodigoDeOperacion == CodOp.LW)
                            {
                                Debug.Print("Núcleo 0: La operación es un LW, por lo que no hay que hacer nada en N1.");
                                Debug.Print("Núcleo 0: Se debe cargar dato desde memoria. Pasando a etapa \"cargar\"...");
                                h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.CARGAR;
                                Monitor.Exit(N1.CacheD.Lock[posicionEnCacheN1]);
                                h[i].Ticks = 40;
                                goto case Hilillo.EtapaSnooping.CARGAR;
                            }
                            else
                            {
                                Debug.Assert(h[i].IR.CodigoDeOperacion == CodOp.SW);
                                Debug.Print("Núcleo 0: La operación es un SW, por lo que invalidamos el bloque en N1.");
                                N1.CacheD.Estado[posicionEnCacheN1] = EstadoDeBloque.I;

                                if(!(CacheD.NumBloque[posicionEnCache] == bloqueDeMemoria && CacheD.Estado[posicionEnCache] == EstadoDeBloque.C))
                                {
                                    Debug.Assert(CacheD.Estado[posicionEnCache] != EstadoDeBloque.M);
                                    Debug.Print("Núcleo 0: Se debe cargar dato desde memoria. Pasando a etapa \"cargar\"...");
                                    h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.CARGAR;
                                    Monitor.Exit(N1.CacheD.Lock[posicionEnCacheN1]);
                                    h[i].Ticks = 40;
                                    goto case Hilillo.EtapaSnooping.CARGAR;
                                }
                                else
                                {
                                    Debug.Print("Núcleo 0: Caso especial de SW con bloque en C."
                                        + " No es necesario cargar el dato de memoria. Pasando a etapa \"después\"...");
                                    Monitor.Exit(N1.CacheD.Lock[posicionEnCacheN1]);
                                    h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                                    goto case Hilillo.EtapaSnooping.DESPUES;
                                }
                            }
                        }
                        else
                        {
                            Debug.Assert(N1.CacheD.Estado[posicionEnCacheN1] == EstadoDeBloque.M);
                            Debug.Print("Núcleo 0: El bloque en N1 está modificado; es necesario copiarlo a memoria y a N0."
                                + " Pasando a etapa \"durante\"...");

                            h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.DURANTE;
                            h[i].Ticks = 40;
                            goto case Hilillo.EtapaSnooping.DURANTE;
                        }
                    }
                case Hilillo.EtapaSnooping.DURANTE:
                    Debug.Print("Núcleo 0: Etapa snooping: DURANTE.");
                    h[i].Ticks--;
                    if (h[i].Ticks > 0)
                    {
                        Debug.Print("Núcleo 0: \"Copiando\" bloque de N1 a memoria y N0. Ticks restantes: " + h[i].Ticks);
                        return;
                    }
                    else
                    {
                        Debug.Assert(h[i].Ticks == 0);
                        Debug.Print("Núcleo 0: Copiando bloque de la posición de caché " + posicionEnCacheN1
                            + " (en N1) a dirección de memoria " + (bloqueDeMemoria * 16) + " (posición de memoria simulada: "
                            + (bloqueDeMemoria * 4) + ") y a la posición de caché " + posicionEnCache + " en N0.");

                        for (int j = 0; j < 4; j++)
                        {
                            CacheD.Cache[j, posicionEnCache] = Memoria.Instance.Mem[bloqueDeMemoria * 4 + j] = N1.CacheD.Cache[j, posicionEnCacheN1];
                        }
                        CacheD.NumBloque[posicionEnCache] = bloqueDeMemoria;

                        if (h[i].IR.CodigoDeOperacion == CodOp.LW)
                        {
                            Debug.Print("Núcleo 0: La instrucción es un LW; el bloque en N1 queda en C.");
                            N1.CacheD.Estado[posicionEnCacheN1] = EstadoDeBloque.C;
                        }
                        else
                        {
                            Debug.Assert(h[i].IR.CodigoDeOperacion == CodOp.SW);
                            Debug.Print("Núcleo 0: La instrucción es un SW; el bloque en N1 queda en I.");
                            N1.CacheD.Estado[posicionEnCacheN1] = EstadoDeBloque.I;
                        }

                        Monitor.Exit(N1.CacheD.Lock[posicionEnCacheN1]);

                        h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                        goto case Hilillo.EtapaSnooping.DESPUES;
                    }

                case Hilillo.EtapaSnooping.CARGAR:
                    Debug.Print("Núcleo 0: Etapa snooping: CARGAR.");
                    h[i].Ticks--;
                    if (h[i].Ticks > 0)
                    {
                        Debug.Print("Núcleo 0: \"Copiando\" bloque de memoria a N0. Ticks restantes: " + h[i].Ticks);
                        return;
                    }
                    else
                    {
                        Debug.Assert(h[i].Ticks == 0);
                        Debug.Print("Núcleo 0: Copiando bloque de la dirección de memoria " 
                            + (bloqueDeMemoria * 16) + " (posición de memoria simulada: "
                            + (bloqueDeMemoria * 4) + ") a la posición de caché " + posicionEnCache + " en N0.");

                        for (int j = 0; j < 4; j++)
                        {
                            CacheD.Cache[j, posicionEnCache] = Memoria.Instance.Mem[bloqueDeMemoria * 4 + j];
                        }
                        CacheD.NumBloque[posicionEnCache] = bloqueDeMemoria;

                        h[i].EtapaDeSnooping = Hilillo.EtapaSnooping.DESPUES;
                        goto case Hilillo.EtapaSnooping.DESPUES;
                    }

                case Hilillo.EtapaSnooping.DESPUES:
                    Debug.Print("Núcleo 0: Etapa snooping: DESPUES.");

                    Debug.Print("Núcleo 0: Terminamos de usar el bus de datos. Se libera.");
                    Monitor.Exit(Memoria.Instance.BusDeDatos);

                    if (h[i].IR.CodigoDeOperacion == CodOp.LW)
                    {
                        Debug.Print("Núcleo 0: La operación es un LW, se copia de posición en caché " + posicionEnCache
                            + ", palabra " + palabra + ", a registro " + X + ". El bloque queda compartido.");
                        h[i].Registro[X] = CacheD.Cache[palabra, posicionEnCache];
                        CacheD.Estado[posicionEnCache] = EstadoDeBloque.C;
                    }
                    else
                    {
                        Debug.Assert(h[i].IR.CodigoDeOperacion == CodOp.SW);
                        Debug.Print("Núcleo 0: La operación es un SW, se copia de registro " + X + " (valor: " + h[i].Registro[X]
                            + ") a posición en caché " + posicionEnCache + ", palabra " + palabra +". El bloque queda modificado.");
                        CacheD.Cache[palabra, posicionEnCache] = h[i].Registro[X];
                        CacheD.Estado[posicionEnCache] = EstadoDeBloque.M;
                        Debug.Print("Núcleo 0: Estado de bloque: " + CacheD.Estado[posicionEnCache]);
                    }

                    Debug.Print("Núcleo 0: Terminamos de usar la caché. Se libera.");
                    Monitor.Exit(CacheD.Lock[posicionEnCache]);

                    CacheD.Reservado[posicionEnCache] = false;
                    busDeDatosReservado = false;

                    h[i].Fase = Hilillo.FaseDeHilillo.Exec;
                    return;
            }
        }

        private void Tick()
        {
            Debug.Print("N0: Entrando a Tick()...\n" +
                "Fase de h[0]: " + h[0].Fase + "\n" +
                "Fase de h[1]: " + h[1].Fase + "\n");

            //aumentar ciclos
            h[0].Ciclos++;
            h[1].Ciclos++;

            //reducir quantums
            if (h[0].Fase == Hilillo.FaseDeHilillo.Exec)
                h[0].Quantum--;
            if (h[1].Fase == Hilillo.FaseDeHilillo.Exec)
                h[1].Quantum--;

            //tabla
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.V && h[0].Fase == Hilillo.FaseDeHilillo.L));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.L && h[0].Fase == Hilillo.FaseDeHilillo.V));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.L && h[0].Fase == Hilillo.FaseDeHilillo.L));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.FI && h[0].Fase == Hilillo.FaseDeHilillo.FI));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.IR && h[0].Fase == Hilillo.FaseDeHilillo.IR));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.FD && h[0].Fase == Hilillo.FaseDeHilillo.L));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.FD && h[0].Fase == Hilillo.FaseDeHilillo.FD));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum > 0 && h[0].Fase == Hilillo.FaseDeHilillo.L));
            Debug.Assert(!(h[1].Quantum == 0 && h[0].Fase == Hilillo.FaseDeHilillo.L));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.Fin && h[0].Fase == Hilillo.FaseDeHilillo.L));
            Debug.Assert(!(h[1].Fase == Hilillo.FaseDeHilillo.Fin && h[0].Fase == Hilillo.FaseDeHilillo.Fin));
            if ((h[0].Fase == Hilillo.FaseDeHilillo.FI ||
                h[0]. Fase == Hilillo.FaseDeHilillo.FD) &&
                h[1].Fase == Hilillo.FaseDeHilillo.V)
            { // H0: V && H1: FI|FD
                lock (ColaHilillos)
                {
                    if (ColaHilillos.Count != 0)
                    {
                        h[1] = ColaHilillos.Dequeue();
                        h[1].Fase = Hilillo.FaseDeHilillo.L;
                        h[1].Quantum = this.Quantum;
                    }
                }
            }
            else if (h[0].Fase == Hilillo.FaseDeHilillo.Exec)
            { // H0: Exec - Mayor riesgo de fracasar
                if (h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum > 0)
                {
                    h[1].Fase = Hilillo.FaseDeHilillo.L;
                }
                else if (h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum == 0)
                {
                    lock (ColaHilillos)
                    {
                        ColaHilillos.Enqueue(h[1]);
                    }
                    h[1] = Hilillo.HililloVacio;
                }
                else if (h[1].Fase == Hilillo.FaseDeHilillo.Fin)
                {
                    lock (HilillosFinalizados)
                    {
                        HilillosFinalizados.Add(h[1]);
                    }
                    h[1] = Hilillo.HililloVacio;
                }

                //solo cuando q==0
                if (h[0].Quantum == 0)
                {
                    lock (ColaHilillos)
                    {
                        ColaHilillos.Enqueue(h[0]);
                        h[0] = ColaHilillos.Dequeue();
                    }
                    h[0].Quantum = this.Quantum;
                }

                //común
                h[0].Fase = Hilillo.FaseDeHilillo.L;
            }
            else if (h[0].Fase == Hilillo.FaseDeHilillo.Fin)
            { // H0: Fin - Riesgo de fracasar
                lock (HilillosFinalizados)
                {
                    HilillosFinalizados.Add(h[0]);
                }
                
                if (h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum > 0)
                {
                    h[1].Fase = Hilillo.FaseDeHilillo.L;
                }
                else if (h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum == 0)
                {
                    lock (ColaHilillos)
                    {
                        ColaHilillos.Enqueue(h[1]);
                        h[0] = ColaHilillos.Dequeue();
                        h[0].Fase = Hilillo.FaseDeHilillo.L;
                        h[0].Quantum = this.Quantum;
                    }
                    h[1] = Hilillo.HililloVacio;
                    goto salida;
                }

                //común
                lock (ColaHilillos)
                {
                    if (ColaHilillos.Count == 0)
                    {
                        h[0] = Hilillo.HililloVacio;
                    }
                    else
                    {
                        h[0] = ColaHilillos.Dequeue();
                        h[0].Fase = Hilillo.FaseDeHilillo.L;
                        h[0].Quantum = this.Quantum;
                    }
                }
            }
            else if (h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum > 0)
            { //H1: Exec, q > 0 && H0: V|L|FI|IR|FD
                h[1].Fase = Hilillo.FaseDeHilillo.L;
            }
            else if (h[1].Fase == Hilillo.FaseDeHilillo.Exec && h[1].Quantum == 0)
            { //H1: Exec, q == 0 && H0: V|L|FI|IR|FD
                if (h[0].Fase == Hilillo.FaseDeHilillo.V)
                {
                    lock (ColaHilillos)
                    {
                        ColaHilillos.Enqueue(h[1]);
                        h[1] = Hilillo.HililloVacio;
                        h[0] = ColaHilillos.Dequeue();
                        h[0].Fase = Hilillo.FaseDeHilillo.L;
                        h[0].Quantum = this.Quantum;
                    }
                }
                else if (h[0].Fase == Hilillo.FaseDeHilillo.L ||
                    h[0].Fase == Hilillo.FaseDeHilillo.IR)
                {
                    lock (ColaHilillos)
                    {
                        ColaHilillos.Enqueue(h[1]);
                    }
                    h[1] = Hilillo.HililloVacio;
                }
                else if (h[0].Fase == Hilillo.FaseDeHilillo.FI ||
                    h[0].Fase == Hilillo.FaseDeHilillo.FD)
                {
                    lock (ColaHilillos)
                    {
                        ColaHilillos.Enqueue(h[1]);
                        h[1] = ColaHilillos.Dequeue();
                    }
                    h[1].Fase = Hilillo.FaseDeHilillo.L;
                    h[1].Quantum = this.Quantum;
                }
            }
            else if (h[1].Fase == Hilillo.FaseDeHilillo.Fin)
            { //H1: Fin && H0: V|L|FI|IR|FD
                lock (HilillosFinalizados)
                {
                    HilillosFinalizados.Add(h[1]);
                }
                
                if (h[0].Fase == Hilillo.FaseDeHilillo.V)
                {
                    lock (ColaHilillos)
                    {
                        if(ColaHilillos.Count == 0)
                        {
                            h[0] = Hilillo.HililloVacio;
                        }
                        else
                        {
                            h[0] = ColaHilillos.Dequeue();
                            h[0].Fase = Hilillo.FaseDeHilillo.L;
                            h[0].Quantum = this.Quantum;
                        }
                    }
                    h[1] = Hilillo.HililloVacio;
                }
                if (h[0].Fase == Hilillo.FaseDeHilillo.IR)
                {
                    h[1] = Hilillo.HililloVacio;
                }
                else if (h[0].Fase == Hilillo.FaseDeHilillo.FI ||
                  h[0].Fase == Hilillo.FaseDeHilillo.FD)
                {
                    lock (ColaHilillos)
                    {
                        if (ColaHilillos.Count == 0)
                        {
                            h[1] = Hilillo.HililloVacio;
                        }
                        else
                        {
                            h[1] = ColaHilillos.Dequeue();
                            h[1].Fase = Hilillo.FaseDeHilillo.L;
                            h[1].Quantum = this.Quantum;
                        }
                    }
                }
            }
            else if(h[0].Fase == Hilillo.FaseDeHilillo.V && h[1].Fase == Hilillo.FaseDeHilillo.V)
            {
                Terminado = true;
            }

            salida:

            //barrera
            Barrera.SignalAndWait();
        }

        // Retorna información general de los hilillos que están corriendo para desplegarla en pantalla durante la ejecución.
        public string PrettyPrintHilillos()
        {
            string output = "\t\tHilillo 0: " + h[0].Nombre + "\n"
                    + "\t\tHilillo 1: " + h[1].Nombre;

            return output;
        }

        // Retorna los contenidos de los registros y las cachés, de forma legible en consola.
        public string PrettyPrintRegistrosYCaches()
        {
            string output = "";

            output += "\t\tRegistros: \n"
                + "\t\t\tHilillo 0:\n"
                + h[0].PrettyPrintRegistrosYCiclos()
                + "\n\t\t\tHilillo 1:\n"
                + h[1].PrettyPrintRegistrosYCiclos()
                + "\n\t\tCachés:\n"
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
                output += CacheI.NumBloque[i] + "\t\t";
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
                    output += CacheD.Cache[i, j] + "\t\t";
                }
                output += "\n";
            }

            for (int i = 0; i < tamanoCache; i++)
            {
                output += CacheD.NumBloque[i] + "\t\t";
            }

            output += "\n";

            for (int i = 0; i < tamanoCache; i++)
            {
                output += CacheD.Estado[i] + "\t\t";
            }

            return output;
        }

        public Queue<Hilillo> ColaHilillos { get; set; }
        public bool Terminado { get; set; }
        public int Quantum { get; set; }
        public Barrier Barrera { get; set; }
        public List<Hilillo> HilillosFinalizados { get; set; }

        private Hilillo[] h;

        public CacheDatos CacheD { get; set; } 
        private CacheInstrucciones CacheI; // Miembro privado, porque nadie va a acceder a ella desde fuera.
        public const int tamanoCache = 8;

        private bool busDeDatosReservado;
        private bool busDeInstruccionesReservado;

        public NucleoMonohilillo N1 { get; set; }
    }
}
