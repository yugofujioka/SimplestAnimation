using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;


/// <summary>
/// Play AnimationClip
/// </summary>
[RequireComponent(typeof(Animator))]
public class SimplestAnimation : MonoBehaviour {
	public AnimationClip[] animationClip = null;
	public bool playAutomatically = true;
	public int playClipIndex = 0;

	private bool initialized = false;
	private Animator animator = null;
	private AnimationClipPlayable[] clips = null;
	private PlayableGraph graph;
	private PlayableOutput output;


	void OnEnable() {
		if (this.playAutomatically) {
			this.Initialize();
			this.Play(this.playClipIndex);
		}
	}

	void OnDisable() {
		if (this.graph.IsValid())
			this.graph.Stop();
	}

	void Start() {
		this.Initialize();
	}

	void OnDestroy() {
		if (this.graph.IsValid())
			this.graph.Destroy();

		this.initialized = false;
	}

	public void Initialize() {
		if (this.initialized)
			return;

		this.animator = this.GetComponent<Animator>();

		Debug.Assert(this.animationClip != null && this.animationClip[0] != null, "No Animation Clips !");
		Debug.Assert(this.animator.runtimeAnimatorController == null, "AnimationController is set......");

		this.graph = PlayableGraph.Create("Animation Player");
		this.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

		this.output = AnimationPlayableOutput.Create(this.graph, this.name, this.animator);

		this.clips = new AnimationClipPlayable[this.animationClip.Length];
		for (int i = 0; i < this.animationClip.Length; ++i) {
			this.clips[i] = AnimationClipPlayable.Create(this.graph, this.animationClip[i]);
			this.clips[i].SetApplyFootIK(false);
			this.clips[i].SetApplyPlayableIK(false);
		}

		this.initialized = true;
	}

	public void Play(int clipIndex) {
		this.output.SetSourcePlayable(this.clips[clipIndex]);

		this.clips[clipIndex].SetTime(0.0);
		this.graph.Play();
	}

	public void Stop() {
		this.graph.Stop();
	}
}
