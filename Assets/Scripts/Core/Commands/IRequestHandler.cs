namespace Codyssey.Core.Commands
{
    public interface IRequestHandler<in TRequest> where TRequest : struct, IRequest
    {
        void Handle(TRequest request);
    }
}
