using UnityEngine;

[CreateAssetMenu(fileName = "NewDinosaur", menuName = "Dinos/DinoBase")]
public class DinoBase : ScriptableObject
{
    [Header("Stats")]
    public float maxHealth = 100f;
    public float speed = 5f;
    public float damage = 10f;

    [Header("AI")]
    public float detectionRange = 15f;
    public float attackRange = 2.5f;
}