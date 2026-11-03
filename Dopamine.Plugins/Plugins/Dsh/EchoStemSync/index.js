/**
 * Host half of the EchoStem sync bundle.
 *
 * EchoStem itself exposes the small local HTTP bridge (only while the user
 * enables "DSH plugin sync support" in its settings), and the browser half
 * talks to that bridge directly. This host row exists so the bundle and its
 * client module are discovered and managed by the Plugin Manager.
 */
export function apply() {}
