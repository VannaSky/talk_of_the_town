using TMPro;
using UnityEngine;

namespace SimpleRuleTiles.DemoScene {
    public class Navigation : MonoBehaviour {
        [SerializeField] private TMP_Text tileName;
        [SerializeField] private GameObject[] tiles;
        private int curTile;

        public void ChangeTile(int dir) {
            tiles[curTile].SetActive(false);

            curTile += dir;
            if (curTile < 0) curTile = tiles.Length - 1;
            else if (curTile > tiles.Length - 1) curTile = 0;

            tileName.text = tiles[curTile].name;
            tiles[curTile].SetActive(true);
        }
    }
}
