using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.ArchitectureAndExecution
{
    /// <summary>
    /// PR 3 — BaselineMode / Execution Mode Toggle Tests
    ///
    /// Tests the baseline mode configuration in ProcessorConfig:
    /// - BaselineMode enum
    /// - ProcessorConfig.ExecutionMode property
    /// - ProcessorConfig.VliwStealEnabled convenience property
    /// - ProcessorConfig.PipelinedFspEnabled property
    /// - Factory methods preserve new defaults
    ///
    /// Target file: ProcessorConfig.cs
    /// Namespace: HybridCPU_ISE.Tests.ArchitectureAndExecution
    /// </summary>
    public class BaselineModeConfigTests
    {
        #region 3.1 BaselineMode Enum

        [Fact]
        public void WhenBaselineModeEnumThenHasTwoValues()
        {
            // Arrange & Act
            var values = System.Enum.GetValues(typeof(BaselineMode));

            // Assert - Enum has exactly FSP_Enabled == 0 and InOrder_Baseline == 1
            Assert.Equal(2, values.Length);
            Assert.Equal(0, (int)BaselineMode.FSP_Enabled);
            Assert.Equal(1, (int)BaselineMode.InOrder_Baseline);
        }

        [Fact]
        public void WhenBaselineModeEnumThenBackingTypeIsByte()
        {
            // Arrange & Act
            var underlyingType = System.Enum.GetUnderlyingType(typeof(BaselineMode));

            // Assert
            Assert.Equal(typeof(byte), underlyingType);
        }

        #endregion

        #region 3.2 ProcessorConfig.ExecutionMode

        [Fact]
        public void WhenDefaultConfigThenExecutionModeIsFspEnabled()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
        }

        [Fact]
        public void WhenSetInOrderBaselineThenExecutionModeReflects()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act
            config.ExecutionMode = BaselineMode.InOrder_Baseline;

            // Assert
            Assert.Equal(BaselineMode.InOrder_Baseline, config.ExecutionMode);

            // Act - Set back to FSP_Enabled
            config.ExecutionMode = BaselineMode.FSP_Enabled;

            // Assert
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
        }

        #endregion

        #region 3.3 ProcessorConfig.VliwStealEnabled (Convenience Property)

        [Fact]
        public void WhenDefaultConfigThenVliwStealEnabledIsTrue()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert
            Assert.True(config.VliwStealEnabled);
        }

        [Fact]
        public void WhenVliwStealEnabledSetFalseThenExecutionModeIsInOrderBaseline()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act
            config.VliwStealEnabled = false;

            // Assert
            Assert.Equal(BaselineMode.InOrder_Baseline, config.ExecutionMode);
            Assert.False(config.VliwStealEnabled);
        }

        [Fact]
        public void WhenVliwStealEnabledSetTrueThenExecutionModeIsFspEnabled()
        {
            // Arrange
            var config = new ProcessorConfig
            {
                ExecutionMode = BaselineMode.InOrder_Baseline
            };

            // Act
            config.VliwStealEnabled = true;

            // Assert
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.True(config.VliwStealEnabled);
        }

        [Fact]
        public void WhenExecutionModeSetInOrderThenVliwStealEnabledIsFalse()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act
            config.ExecutionMode = BaselineMode.InOrder_Baseline;

            // Assert
            Assert.False(config.VliwStealEnabled);
        }

        [Fact]
        public void WhenExecutionModeSetFspEnabledThenVliwStealEnabledIsTrue()
        {
            // Arrange
            var config = new ProcessorConfig
            {
                ExecutionMode = BaselineMode.InOrder_Baseline
            };

            // Act
            config.ExecutionMode = BaselineMode.FSP_Enabled;

            // Assert
            Assert.True(config.VliwStealEnabled);
        }

        #endregion

        #region 3.4 ProcessorConfig.PipelinedFspEnabled

        [Fact]
        public void WhenDefaultConfigThenPipelinedFspEnabledIsFalse()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert
            Assert.False(config.PipelinedFspEnabled);
        }

        [Fact]
        public void WhenPipelinedFspSetTrueThenPropertyReflects()
        {
            // Arrange
            var config = new ProcessorConfig();

            // Act
            config.PipelinedFspEnabled = true;

            // Assert
            Assert.True(config.PipelinedFspEnabled);

            // Act - Set back to false
            config.PipelinedFspEnabled = false;

            // Assert
            Assert.False(config.PipelinedFspEnabled);
        }

        [Fact]
        public void WhenHighPerformanceFPGAConfigThenPipelinedFspDefaultFalse()
        {
            // Arrange & Act
            var config = ProcessorConfig.HighPerformanceFPGA();

            // Assert - Factory method doesn't override PipelinedFspEnabled default
            Assert.False(config.PipelinedFspEnabled);
        }

        #endregion

        #region 3.5 Factory Methods Preserve New Defaults

        [Fact]
        public void WhenDefaultFactoryThenAllNewFieldsHaveDefaults()
        {
            // Arrange & Act
            var config = ProcessorConfig.Default();

            // Assert
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.True(config.VliwStealEnabled);
            Assert.False(config.PipelinedFspEnabled);
        }

        [Fact]
        public void WhenTestingFactoryThenAllNewFieldsHaveDefaults()
        {
            // Arrange & Act
            var config = ProcessorConfig.Testing();

            // Assert
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.True(config.VliwStealEnabled);
            Assert.False(config.PipelinedFspEnabled);
        }

        [Fact]
        public void WhenProfilingFactoryThenAllNewFieldsHaveDefaults()
        {
            // Arrange & Act
            var config = ProcessorConfig.Profiling();

            // Assert
            Assert.Equal(BaselineMode.FSP_Enabled, config.ExecutionMode);
            Assert.True(config.VliwStealEnabled);
            Assert.False(config.PipelinedFspEnabled);
        }

        #endregion
    }
}
