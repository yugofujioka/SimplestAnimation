using UnityEngine;


[RequireComponent(typeof(Animator))]
public class SampleSimplestAnimator : MonoBehaviour {
	public AnimationClip[] animationClip = null;
	public bool playAutomatically = true;
	[Range(1, 16)]
	public int maxQueue = 8;

	private SimplestAnimationState anim = new SimplestAnimationState();

	void OnEnable() {
		if (this.playAutomatically) {
			this.anim.Initialize(this.GetComponent<Animator>(), this.animationClip, this.maxQueue);
			this.anim.Play(0);
		}
	}

	void OnDisable() {
		if (this.anim.isPlaying)
			this.anim.Stop();
	}

	void Start() {
		this.anim.Initialize(this.GetComponent<Animator>(), this.animationClip, this.maxQueue);
	}

	void OnDestroy() {
		this.anim.Release();
	}

	void Update() {
		if (Input.GetMouseButtonDown(0)) {
			this.anim.Play(1, 0.2f);
			this.anim.PlayQueued(2, 0f);
			this.anim.PlayQueued(0, 0.4f);
		}
		*/

		var elapsedTime = Time.deltaTime;
		this.anim.Proc(elapsedTime);
	}
}
