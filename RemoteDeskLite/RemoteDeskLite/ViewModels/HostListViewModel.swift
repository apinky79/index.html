import Combine
import Foundation

@MainActor
final class HostListViewModel: ObservableObject {
    @Published var searchText = ""
    @Published var selectedHost: RemoteHost?
    @Published var hostToEdit: RemoteHost?
    @Published var isPresentingNewHost = false

    private let store: HostStore
    private var cancellables: Set<AnyCancellable> = []

    init(store: HostStore) {
        self.store = store

        store.objectWillChange
            .sink { [weak self] _ in
                self?.objectWillChange.send()
            }
            .store(in: &cancellables)
    }

    var hosts: [RemoteHost] {
        let base = store.sortedHosts()
        let trimmedSearch = searchText.trimmingCharacters(in: .whitespacesAndNewlines)

        guard !trimmedSearch.isEmpty else {
            return base
        }

        return base.filter { host in
            host.name.localizedCaseInsensitiveContains(trimmedSearch)
                || host.address.localizedCaseInsensitiveContains(trimmedSearch)
                || host.protocolType.rawValue.localizedCaseInsensitiveContains(trimmedSearch)
        }
    }

    func save(_ host: RemoteHost) {
        store.upsert(host)
    }

    func delete(_ host: RemoteHost) {
        store.delete(host)
    }

    func markConnected(_ host: RemoteHost) {
        store.markConnected(hostID: host.id)
    }
}
