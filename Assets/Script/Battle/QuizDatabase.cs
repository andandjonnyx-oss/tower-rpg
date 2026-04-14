using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/QuizDatabase")]
public class QuizDatabase : ScriptableObject
{
    [Header("クイズ問題リスト")]
    [Tooltip("この中からランダムに出題される。重複なし。")]
    public List<QuizData> quizzes = new List<QuizData>();
}