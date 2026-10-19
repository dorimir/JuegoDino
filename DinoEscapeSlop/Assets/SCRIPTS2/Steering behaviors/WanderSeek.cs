using UnityEngine;

public class WanderSeek : MonoBehaviour
{
            private Vector3 target;

            private bool Arrival = false; //si es true, el enemigo se detiene en el centro del nodo, si es false, el enemigo sigue avanzando hasta el siguiente nodo
            
            //follow strength, higher = faster, smaller arc;
            public float followStrength = 0.5f; //0.5 default

            private float repel = 0.5f; // fuerza de repulsion, mayor = mas fuerte rango 0-1

            private float radius = 10f; // cada cuadricula de unity son 10m

            private float maxWanderStrength = 1f; //0-1, 1 significa que el angulo de wander es igual al ratio distancia

            private float step = 45f; //numero de grados por el que variará el ángulo en cada frame;

            private Vector3 counterforce = new Vector3(0,0,0);
            private Vector3 force = new Vector3(0,0,0);
            private Vector3 desired = new Vector3(0,0,0);

            private Vector3 wander = new Vector3(0,0,0);

            //MAXSPEED rotation interpolation factor
            private float maxspeed = 2.5F; 

            private bool wandering = true;
            
         // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        }

        // Update is called once per frame
        void FixedUpdate()
        {   
            if(wandering)
            {
                wanderRotation(target);
            }
            else rotation(target);
            
            //forceForward(target);
        }

        public void setTarget(Vector3 newTarget)
        {
            target = newTarget;
            //wander inicial es igual a desired
            wander = target - this.transform.position;
        }

        public void setArrival(bool newArrival)
        {
            Arrival = newArrival;
        }

        public void setWanderState(bool newWandering)
        {
            wandering = newWandering;
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

        private void wanderRotation(Vector3 targetPosition)
        {
            
            if(targetPosition == null) return;
            //ROTATION
            //1.DESIRED
            desired = targetPosition - this.transform.position;

            if(!Arrival)
            {
                 //calculamos el radio de wander
                float distance = Mathf.Sqrt(desired.x*desired.x + desired.z*desired.z);
                if(distance<0) distance = -distance; //asegura que la distancia sea positiva
                float wanderRatio = (1f-(distance/100f))*distance;

                //calculamos su angulo
                float wanderRatioAngle = Mathf.Atan(wanderRatio/distance);

                //valor siempre positivo
                if(wanderRatioAngle<0)
                {
                    wanderRatioAngle= -wanderRatioAngle;
                }

                //calculamos el maximo angulo de desviación siempre positivo
                float maxWanderStrengthAngle = wanderRatioAngle * maxWanderStrength;

                //decidimos la variación frame a frame
                float rand = Random.Range(0,1);
                float newstep;
                if(rand==0)
                {
                    newstep = step;
                }
                else
                {
                newstep = -step; 
                }
                
                //creamos el nuevo vector wander
                float newx;
                newx = Mathf.Cos(newstep)*wander.x -Mathf.Sin(newstep)*wander.z;
                float newz;
                newz= Mathf.Sin(newstep)*wander.x +Mathf.Cos(newstep)*wander.z;
                Vector3 newWander= new Vector3(newx, wander.y, newz);
                Vector3 desired_wander = new Vector3(newWander.x/desired.x, newWander.y/desired.y, newWander.z/desired.z);
                
                //comprobamos no excedir el ángulo máximo
                float wAngle = Mathf.Acos( Mathf.Sqrt(desired_wander.x*desired_wander.x + desired_wander.z*desired_wander.z));
                if(wAngle<0)
                {
                    wAngle = -wAngle;
                }

                //lo corregimos de haverlo superado
                if(wAngle>maxWanderStrengthAngle)
                {
                    if(newstep>0)
                    {
                         //creamos el nuevo vector wander
                        newx = Mathf.Cos(maxWanderStrengthAngle)*desired.x -Mathf.Sin(maxWanderStrengthAngle)*desired.z;
                        newz = Mathf.Sin(maxWanderStrengthAngle)*desired.x +Mathf.Cos(maxWanderStrengthAngle)*desired.z;
                        newWander= new Vector3(newx, wander.y, newz);
                    }
                    else
                    {
                        newx = Mathf.Cos(-maxWanderStrengthAngle)*desired.x -Mathf.Sin(-maxWanderStrengthAngle)*desired.z;
                        newz = Mathf.Sin(-maxWanderStrengthAngle)*desired.x +Mathf.Cos(-maxWanderStrengthAngle)*desired.z;
                        newWander= new Vector3(newx, wander.y, newz);
                    }
                       
                }
                //actualizamos wander para continuar variando sobre él
                wander = newWander;

                //Rotation
                Quaternion targetRotation = Quaternion.LookRotation(wander);

                //interpolación
                this.transform.rotation = Quaternion.Slerp
                (
                    this.transform.rotation, // Current rotation
                    targetRotation,     // Target rotation
                    Time.deltaTime * maxspeed // Interpolation factor
                );

            }
            else
            {
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
        }

        private void forceForward(Vector3 targetPosition)
        {
            if(targetPosition == null) return;
            //FORCE FORWARD
            force = Vector3.Lerp(this.transform.position, targetPosition, followStrength * Time.deltaTime);

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

