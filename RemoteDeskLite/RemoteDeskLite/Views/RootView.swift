import SwiftUI

struct RootView: View {
    @ObservedObject var store: HostStore
    let passwordStore: PasswordStore

    var body: some View {
        TabView {
            HostListView(store: store, passwordStore: passwordStore)
                .tabItem {
                    Label("Hosts", systemImage: "desktopcomputer")
                }

            SettingsView()
                .tabItem {
                    Label("Settings", systemImage: "gearshape")
                }
        }
    }
}
