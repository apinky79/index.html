import SwiftUI

@main
struct RemoteDeskLiteApp: App {
    @StateObject private var hostStore = HostStore()
    private let passwordStore: PasswordStore = KeychainPasswordStore()

    var body: some Scene {
        WindowGroup {
            RootView(store: hostStore, passwordStore: passwordStore)
        }
    }
}
