using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Loads the real GreyboxTest scene (Cubism standee, URP shader, Cinemachine rig and all)
// and runs it for a few frames. Unity's test runner fails the test on any unexpected
// Debug.LogError/exception, so this is the automated stand-in for "does it even run
// without erroring" when nobody can eyeball the Editor - it does not confirm the standee
// looks correct, only that nothing throws or logs an error while it's alive.
public class GreyboxSceneSmokeTest
{
    [UnityTest]
    public IEnumerator GreyboxTestScene_LoadsAndRunsWithoutErrors()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;

        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        Assert.Pass();
    }
}
