using System.Threading.Channels;

namespace Marille;

internal record TopicInfo<T> (TopicConfiguration Configuration, Channel<T> Channel);
