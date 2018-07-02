using System;

namespace SimuladorMIPS
{
    enum EstadoDeBloque { I, M, C }

    struct CacheDatos
    {
        // TIP: Ver diferencia entre "jagged array" y "multidimensional array".
        public int[,] Cache;
        public int[] NumBloque;
        public EstadoDeBloque[] Estado;
        public bool[] Reservado;
        public Object[] Lock;

        public CacheDatos(int tamano)
        {
            Cache = new int[4, tamano];
            NumBloque = new int[tamano];
            Estado = new EstadoDeBloque[tamano];
            Reservado = new bool[tamano];
            Lock = new Object[tamano];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < tamano; j++)
                {
                    Cache[i, j] = 0;
                }
            }

            for (int i = 0; i < tamano; i++)
            {
                NumBloque[i] = 0;
                Estado[i] = EstadoDeBloque.I;
                Reservado[i] = false;
                Lock[i] = new Object();
            }
        }
    }
}