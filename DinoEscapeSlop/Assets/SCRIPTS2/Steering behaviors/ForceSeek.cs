using UnityEngine;

public class ForcesSeek : MonoBehaviour
{
            private Vector3 target;

            private bool Arrival = false; //si es true, el enemigo se detiene en el centro del nodo, si es false, el enemigo sigue avanzando hasta el siguiente nodo
            
            //follow strength, higher = faster, smaller arc;
            private float smooothness = 0.5f;

            private float repel = 0.5f; // fuerza de repulsion, mayor = mas fuerte rango 0-1

            private float radius = 10f; // cada cuadricula de unity son 10m

            private Vector3 counterforce = new Vector3(0,0,0);
            private Vector3 force = new Vector3(0,0,0);
            private Vector3 desired = new Vector3(0,0,0);

            //MAXSPEED rotation interpolation factor
            private float maxspeed = 2.5F; 
            
         // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start(){ }

        // Update is called once per frame
        void FixedUpdate()
        {
            rotation(target);
            forceForward(target);
        }

        public void setTarget(Vector3 newTarget)
        {
            target = newTarget;
        }

        public void setArrival(bool newArrival)
        {
            Arrival = newArrival;
        }

        private void rotation(Vector3 targetPosition)
        {
            if(targetPosition == null) return;
            //ROTATION
            //1.DESIRED
            desired = targetPosition - this.transform.position;

            //TURNING
             // Generate a Quaternion that faces that direction
            // Passing the up direction (Vector3.up) as the second argument keeps the head from tilting NO HACE FALTA
            Quaternion targetRotation = Quaternion.LookRotation(desired);

           
            // 2. Interpolate between the current and target rotations with Slerp
            // Moving a fixed fraction toward the target each frame produces
            // a natural turn that eases out near the end
            this.transform.rotation = Quaternion.Slerp
            (
                this.transform.rotation, // Current rotation
                targetRotation,     // Target rotation
                Time.deltaTime * maxspeed // Interpolation factor
            );
        }

        private void forceForward(Vector3 targetPosition)
        {
            if(targetPosition == null) return;
            //FORCE FORWARD
            force = Vector3.Lerp(this.transform.position, targetPosition, smooothness * Time.deltaTime);

            if(force.z<0) //asegura que la fuerza sea positiva
            {
                force = new Vector3(0, 0, -force.z);
            }
            else
            {
                force = new Vector3(0, 0, force.z);
            }


            if(Arrival)
            {
                //ARRIVAL (balance de fuerzas en el centro, contrafuerza progresivamente superior a la fuerza de empuje) 
                

                //distancia absoluta
                desired = targetPosition - this.transform.position;
                float distance = Mathf.Sqrt(desired.x*desired.x + desired.z*desired.z);
                if(distance<0) distance = -distance; //asegura que la distancia sea positiva
                
                if(distance<radius)
                {
                    // Rebajar ambas fuerzas progresivamente
                    Debug.Log("Ratio: " + (1f- distance / radius )); //Range 0-1, 0 = center, 1 = radius
                    float ratio = distance/radius; //Range 0-1, 0 = center, 1 = radius
                    //TODO Rebajar ambas fuerzas progresivamente
                    counterforce = new Vector3(0, 0, force.z* (1f+(1 - ratio))*repel); 
                    force = new Vector3(0, 0, force.z);
                    this.GetComponent<ConstantForce>().relativeForce = (force - counterforce)*ratio; 

                }
                else this.GetComponent<ConstantForce>().relativeForce = force; 
            }
            else this.GetComponent<ConstantForce>().relativeForce = force; 
        }
}

