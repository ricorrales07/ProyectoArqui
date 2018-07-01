using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SimuladorMIPS
{
    class Hilillo
    {
        // WARNING: Para evitar problemas de inconsistencia, usar este hilillo siempre que se necesite un hilillo vacío.
        private static readonly Hilillo hililloVacio = new Hilillo(FaseDeHilillo.V, 0, "vacío");
        public static Hilillo HililloVacio
        {
            get
            {
                return hililloVacio;
            }
        }

        private Hilillo(FaseDeHilillo fase, int direccionDeInicio, string nombre)
        {
            PC = direccionDeInicio;
            this.Nombre = nombre;
            IR = new Instruccion(0);
            Debug.Assert(IR.Operando[0] == 0);
            Registro = new int[32];
            Ciclos = 0;
            Fase = fase;
        }

        public Hilillo(int direccionDeInicio, string nombre)
        {
            PC = direccionDeInicio;
            this.Nombre = nombre;
            IR = new Instruccion(0);
            Debug.Assert(IR.Operando[0] == 0);
            Registro = new int[32];
            Ciclos = 0;
            Fase = FaseDeHilillo.L;
        }

        // Esta función se llama al final para imprimir datos de un hilillo finalizado.
        public string PrettyPrintRegistrosYCiclos()
        {
            string output = "";

            output += Nombre + ":\n"
                + "Ciclos: " + Ciclos + "\n"
                + "Quantum: " + Quantum + "\n"
                + "Registros: ";

            for (int i = 0; i < 32; i++)
            {
                output += Registro[i] + " ";
            }

            return output;
        }

        public int PC { get; set; }
        public Instruccion IR { get; set; }
        public int[] Registro { get; set; }
        public int Quantum { get; set; }

        public enum EtapaSnooping { ANTES, DURANTE, CARGAR, DESPUES }

        public bool Recursos { get; set; }
        public int Ticks { get; set; }
        public EtapaSnooping EtapaDeSnooping { get; set; }

        public string Nombre { get; }
        public int Ciclos;

        public enum FaseDeHilillo { V, L, FI, IR, FD, Exec, Fin }

        public FaseDeHilillo Fase;
    }
}
