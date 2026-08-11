import SwiftUI

struct SettingsView: View {
    @AppStorage("launchAtBoot") private var launchAtBoot = false
    @AppStorage("showPerformanceOverlay") private var showPerformanceOverlay = true
    @AppStorage("enableHaptics") private var enableHaptics = true
    @AppStorage("preferredCodec") private var preferredCodec = "Auto"
    @AppStorage("networkQuality") private var networkQuality = "Balanced"

    private let codecOptions = ["Auto", "H.264", "HEVC"]
    private let qualityOptions = ["Best Quality", "Balanced", "Low Latency"]

    var body: some View {
        NavigationStack {
            Form {
                Section("General") {
                    Toggle("Launch at startup", isOn: $launchAtBoot)
                    Toggle("Show performance overlay", isOn: $showPerformanceOverlay)
                    Toggle("Enable haptics", isOn: $enableHaptics)
                }

                Section("Streaming") {
                    Picker("Preferred codec", selection: $preferredCodec) {
                        ForEach(codecOptions, id: \.self) { option in
                            Text(option).tag(option)
                        }
                    }

                    Picker("Network profile", selection: $networkQuality) {
                        ForEach(qualityOptions, id: \.self) { option in
                            Text(option).tag(option)
                        }
                    }
                }

                Section("About") {
                    LabeledContent("App", value: "RemoteDeskLite")
                    LabeledContent("Version", value: "1.0")
                    Text("This build contains a mock streaming engine and is ready for plugging in real RDP/VNC transports.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle("Settings")
        }
    }
}
