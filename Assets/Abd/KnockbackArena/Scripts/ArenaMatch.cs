using System.Collections.Generic;
using UnityEngine;

namespace Abd.KnockbackArena
{
    // Elenme sırasını tutar. Party tarafı sıralamayı buradan okuyacak (ilk elenen en son sırada).
    public class ArenaMatch : MonoBehaviour
    {
        readonly List<EliminationOnFall> alive = new List<EliminationOnFall>();
        readonly List<string> eliminationOrder = new List<string>();

        public IReadOnlyList<string> EliminationOrder => eliminationOrder;

        void Start()
        {
            foreach (EliminationOnFall e in FindObjectsByType<EliminationOnFall>(FindObjectsSortMode.None))
            {
                alive.Add(e);
                e.Eliminated += OnEliminated;
            }
        }

        void OnEliminated(EliminationOnFall e)
        {
            alive.Remove(e);
            eliminationOrder.Add(e.name);
            Debug.Log($"{e.name} elendi. Kalan: {alive.Count}");
            if (alive.Count == 1) Debug.Log($"{alive[0].name} kazandı.");
        }
    }
}
