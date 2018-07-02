using System;

namespace SimuladorMIPS
{
    struct Instruccion
    {
        public CodOp CodigoDeOperacion;
        public int[] Operando;

        public Instruccion (int dummy = 0)
        {
            CodigoDeOperacion = 0;
            Operando = new int[] { 0, 0, 0 };
        }

        public Instruccion (Instruccion i)
        {
            this.CodigoDeOperacion = i.CodigoDeOperacion;
            this.Operando = new int[3];
            for (int j = 0; j < 3; j++)
            {
                this.Operando[j] = i.Operando[j];
            }
        }
    }

    struct CacheInstrucciones
    {
        // TIP: Ver diferencia entre "jagged array" y "multidimensional array".
        public Instruccion[,] Cache;
        public int[] NumBloque;
        public bool[] Reservado;
        public Object[] Lock;

        public CacheInstrucciones(int tamano)
        {
            Cache = new Instruccion[4, tamano];
            NumBloque = new int[tamano];
            Reservado = new bool[tamano];
            Lock = new Object[tamano];

            for (int i = 0; i < tamano; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    Cache[j, i] = new Instruccion(0);
                }
                NumBloque[i] = 0;
                Reservado[i] = false;
                Lock[i] = new Object();
            }
        }
    }
}