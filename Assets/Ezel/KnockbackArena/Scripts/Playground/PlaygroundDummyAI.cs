using UnityEngine;

namespace KnockbackArena.Playground
{
    // Sparring partner for the playground: wanders near home and spits at the target now and then.
    [RequireComponent(typeof(CritterMotor), typeof(CritterSpit))]
    public class PlaygroundDummyAI : MonoBehaviour
    {
        public Transform target;
        public Vector2 spitInterval = new Vector2(1.6f, 3.4f);
        public float range = 9f, wanderRadius = 2.2f;

        CritterMotor motor;
        CritterSpit spit;
        Vector3 home, goal;
        float nextSpit, nextGoal;

        void Awake()
        {
            motor = GetComponent<CritterMotor>();
            spit = GetComponent<CritterSpit>();
            home = goal = transform.position;
            nextSpit = Time.time + Random.Range(spitInterval.x, spitInterval.y);
        }

        void Update()
        {
            if (Time.time > nextGoal)
            {
                var r = Random.insideUnitCircle * wanderRadius;
                goal = home + new Vector3(r.x, 0f, r.y);
                nextGoal = Time.time + Random.Range(1.5f, 3.5f);
            }
            var to = goal - transform.position; to.y = 0f;
            motor.Move(to.magnitude > 0.4f ? to.normalized * 0.6f : Vector3.zero);

            if (!target || Time.time < nextSpit) return;
            var aim = target.position - transform.position; aim.y = 0f;
            if (aim.magnitude < range) spit.TryFire(aim);
            nextSpit = Time.time + Random.Range(spitInterval.x, spitInterval.y);
        }
    }
}
