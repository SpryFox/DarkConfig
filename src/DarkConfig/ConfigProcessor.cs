#nullable enable

namespace DarkConfig {
    /// Processors can modify DocNodes after being parsed from YAML, but before used by the rest of the systems
    /// Note: this is ONLY run on the YAML -> DocNode path, any other DocNode creation is not affected
    public interface ConfigProcessor {
        /// Does this processor potentially mutate data
        /// Will make sure ComposedDocNode.MakeMutable() is run at least once before running this processor
        public bool CanMutate { get; }

        /// Process the DocNode here
        public void Process(string filename, ref DocNode doc);
    }
}
