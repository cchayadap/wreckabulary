# Root integration review

STATUS: SOURCE REVIEW COMPLETE; native capture pending.

Reviewed the complete candidate fixture and its explicit test-assembly reference. It invokes the actual menu OnGUI inside the existing GameViewScope and captures the rendered framebuffer. Seven wide/narrow states plus two targeted narrow views produce sixteen expected images. Coordinator responses are strictly in memory, each request path is recorded, the menu Update is disabled, and gameplay launch explicitly fails. The UI FIXTURE marker and receipt distinguish these states from a real online match.

The fixture preserves source/theme/dependency hashes and metadata, borrowed theme identity/dirty state, lobby theme preference and selected theme. Its owned alternate font profile, RSA instance, camera/root and coordinator are disposed; GameView/async shader compilation/time scale are restored through guarded cleanup. No saved asset or service write is introduced. R0 183303 passes after integration.

No source blocker found for a bounded native capture. Required control registration is not a visibility or accessibility measurement: root must open the images and inspect overlap, clipping, scroll position, contrast and readable error/action states. The successful final receipt must include cleanup and protected-file comparisons. Physical input, private assignment/real room lifecycle and public gameplay remain separate acceptance.
