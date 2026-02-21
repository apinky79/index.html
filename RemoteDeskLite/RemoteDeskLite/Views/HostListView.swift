import SwiftUI

struct HostListView: View {
    @StateObject private var viewModel: HostListViewModel
    let passwordStore: PasswordStore

    init(store: HostStore, passwordStore: PasswordStore) {
        _viewModel = StateObject(wrappedValue: HostListViewModel(store: store))
        self.passwordStore = passwordStore
    }

    var body: some View {
        NavigationStack {
            Group {
                if viewModel.hosts.isEmpty {
                    ContentUnavailableView(
                        "No Saved Hosts",
                        systemImage: "desktopcomputer",
                        description: Text("Add an RDP or VNC endpoint to start a remote session.")
                    )
                } else {
                    List {
                        ForEach(viewModel.hosts) { host in
                            Button {
                                viewModel.selectedHost = host
                            } label: {
                                HostRowView(host: host)
                            }
                            .buttonStyle(.plain)
                            .swipeActions(edge: .leading, allowsFullSwipe: true) {
                                Button {
                                    viewModel.selectedHost = host
                                } label: {
                                    Label("Connect", systemImage: "play.fill")
                                }
                                .tint(.green)
                            }
                            .swipeActions(edge: .trailing, allowsFullSwipe: true) {
                                Button(role: .destructive) {
                                    viewModel.delete(host)
                                } label: {
                                    Label("Delete", systemImage: "trash")
                                }
                            }
                            .contextMenu {
                                Button("Connect", systemImage: "play.fill") {
                                    viewModel.selectedHost = host
                                }
                                Button("Edit", systemImage: "pencil") {
                                    viewModel.hostToEdit = host
                                }
                                Divider()
                                Button("Delete", systemImage: "trash", role: .destructive) {
                                    viewModel.delete(host)
                                }
                            }
                        }
                    }
                    .listStyle(.plain)
                }
            }
            .navigationTitle("Remote Hosts")
            .searchable(text: $viewModel.searchText, prompt: "Search by name, IP, protocol")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        viewModel.isPresentingNewHost = true
                    } label: {
                        Label("Add Host", systemImage: "plus")
                    }
                }
            }
            .sheet(isPresented: $viewModel.isPresentingNewHost) {
                AddHostView { host in
                    viewModel.save(host)
                }
            }
            .sheet(item: $viewModel.hostToEdit) { host in
                AddHostView(existingHost: host) { updatedHost in
                    viewModel.save(updatedHost)
                }
            }
            .fullScreenCover(item: $viewModel.selectedHost) { host in
                ConnectionView(host: host, passwordStore: passwordStore) {
                    viewModel.markConnected(host)
                }
            }
        }
    }
}
