using System.Threading.Tasks;
using UnityEngine;

public class RegisterAllPawn : MonoBehaviour
{
    public string Tag;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    private void Start()
    {
        
        if (!SpatialGrid.IsCreate) SpatialGrid.Initialize(10);

        waitForSpawn();
    }

    private async void waitForSpawn()
    {
        await Awaitable.NextFrameAsync();

        foreach (var v in GameObject.FindGameObjectsWithTag(Tag))
        {
            SpatialGrid.Register(v.transform);  
        }
    }
}
