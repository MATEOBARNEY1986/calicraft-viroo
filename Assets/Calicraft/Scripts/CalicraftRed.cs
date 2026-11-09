using UnityEngine;
using Viroo.Interactions;

// La red de Calicraft en VIROO: cada cambio del mundo viaja como un texto corto a todos los jugadores.
//   "r|x|y|z"        se rompió el bloque de esa celda
//   "p|x|y|z|tipo"   se puso un bloque (tipo = número del enum B)
//   "m|i"            se encontró la memoria número i
// Execute(...) lo corre en TODOS (también en quien lo envía). Con persistencia "All", el servidor guarda
// la lista completa y, a quien entra tarde, VIROO le llama RestoreState una vez por cada mensaje, en orden:
// así reconstruye el mismo mundo. Va en el mismo GameObject que Mundo (escena Calicraft, dentro de Root).
public class CalicraftRed : BroadcastObjectAction
{
    public static CalicraftRed Instancia { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Instancia = this;
    }

    protected override void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        base.OnDestroy();
    }

    // un jugador hizo un cambio: se aplica con sonido y efectos
    protected override void LocalExecuteImplementation(string data) => Mundo.Recibir(data, true);

    // reconstrucción al entrar tarde: se aplica en silencio (sin sonidos, sin paneles)
    public override void RestoreState(string data) => Mundo.Recibir(data, false);
}
